using MauiAppMain.Models;
using MauiAppMain.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
public class DataFetch
{
    private readonly IDeviceInfoService _deviceInfoService;
    private readonly DatabaseService _database;
    private readonly HttpClient _httpClient;

    public event Action? OnDataUpdated;

    public DataFetch(IDeviceInfoService deviceInfoService, DatabaseService databaseService, HttpClient httpClient)
    {
        _deviceInfoService = deviceInfoService;
        _database = databaseService;
        _httpClient = httpClient;
    }
    public async Task FetchData(bool forlang)
    {
        // Build URL with lastSyncTime if EXITSTS
        var url = await BuildPoiRequestUrl(Preferences.Get("LastSyncTime", 0L), forlang);
        // SEND REQUEST
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("API request failed");
            return;
        }
        Console.WriteLine("API request suscess");
        if (IsLowStorage())
        {
            Console.WriteLine("Device is low on storage, requesting data with language filter");
        }
        else
        {
            Console.WriteLine("Device has sufficient storage, requesting all data");
        }
        // Read response
        var json = await response.Content.ReadAsStringAsync();
        
        // 🔥 DEBUG 1: SEE THE RAW TEXT
        Console.WriteLine("[RAW JSON PRE-PARSE]: " + (json.Length > 200 ? json.Substring(0, 200) : json));

        //  0. CONFIGURE DESERIALIZER (Resilient to case sensitivity)
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Deserialize
        var result = JsonSerializer.Deserialize<PoiApiResponse>(json, options);
        
        if (result != null)
        {
            Console.WriteLine($"[SYNC DEBUG] Data parsed: {result.Pois?.Count ?? 0} POIs, {result.DeletedIds?.Count ?? 0} Deleted IDs");
        }

        // Check if data is updated
        if (result == null || !result.Updated)
        {
            Console.WriteLine("No new data");
            OnDataUpdated?.Invoke();
            return;
        }
        var prettyJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        Console.WriteLine("new data.");
        Console.WriteLine(prettyJson);

        var deletedPoiIds = new HashSet<int>();

        // 1. DELETE
        if (result.DeletedIds != null && result.DeletedIds.Count > 0)
        {
            foreach (var id in result.DeletedIds)
            {
                if (int.TryParse(id, out int poiId))
                {
                    await _database.DeletePOIAsync(poiId);
                    deletedPoiIds.Add(poiId);
                    Console.WriteLine($"[DELETE SUCCESS] POI {poiId} removed from Local DB");
                }
            }
        }
        // 2. SAVE LANGUAGE OPTIONS
        if (result.Languages != null)
        {
            foreach (var lang in result.Languages)
            {
                await _database.AddLanguageAsync(lang);
            }
        }
        var Lnag = await _database.GetLanguagesAsync();
        foreach (var item in Lnag)
        {
            Console.WriteLine("Language: " + item.Language + " - Code: " + item.Code);
        }
        //  2. SAVE POIs AND HANDLE DELETIONS FROM POI LIST
        if (result.Pois != null)
        {
            var activePois = new List<Poi>();
            foreach (var poi in result.Pois)
            {
                if (poi.IsDeleted || deletedPoiIds.Contains(poi.Id))
                {
                    // POI was marked as deleted by the backend. Delete locally.
                    await _database.DeletePOIAsync(poi.Id);
                    deletedPoiIds.Add(poi.Id);
                    Console.WriteLine($"[DELETE SUCCESS] POI {poi.Id} removed from Local DB via IsDeleted flag");
                }
                else
                {
                    activePois.Add(poi);
                }
            }

            if (activePois.Count > 0)
            {
                await _database.SavePoisAsync(activePois);
            }
        }
        //  3. SAVE DESCRIPTIONS
        if (result.Descriptions != null)
        {
            var activeDescriptions = result.Descriptions.Where(d => !deletedPoiIds.Contains(d.PoiId)).ToList();
            if (activeDescriptions.Count > 0)
            {
                await _database.SaveDescriptionsAsync(activeDescriptions);
            }
        }
        var des = await _database.GetAllDescriptionsAsync();
        foreach (var item in des)
        {
            Console.WriteLine("Description: " + item.Name + " - Id: " + item.PoiId);
        }

        //  4. HANDLE IMAGES
        if (result.Images != null)
        {
            foreach (var kvp in result.Images)
            {
                if (!int.TryParse(kvp.Key, out int poiId)) continue;
                
                var urls = kvp.Value;

                // 🔥 1. Purge existing image records and physical files for this POI
                await _database.DeleteImagesByPoiIdAsync(poiId);
                
                // 🔥 2. Also ensure the folder is empty of any "ghost" files not in the DB
                string folder = Path.Combine(FileSystem.AppDataDirectory, $"POI_{poiId}");
                if (Directory.Exists(folder))
                {
                    try { Directory.Delete(folder, true); } catch { }
                    Directory.CreateDirectory(folder);
                }

                if (urls == null || urls.Count == 0) continue;

                // 🔥 3. Download and store new images
                foreach (var urlItem in urls)
                {
                    if (string.IsNullOrWhiteSpace(urlItem) || urlItem.EndsWith("/")) continue;

                    var localPath = await DownloadImageAsync(poiId, urlItem);
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        await _database.AddImageAsync(poiId, localPath);
                    }
                }
            }
        }

        //  5. UPDATE LAST SYNC (ONLY if it was a general sync)
        if (!forlang)
        {
            Preferences.Set("LastSyncTime", result.LastUpdated);
            Console.WriteLine("Last update: " + result.LastUpdated);
        }

        // 6. NOTIFY UI
        OnDataUpdated?.Invoke();
    }

    //hàm thêm ảnh
    private async Task<string> DownloadImageAsync(int poiId, string url)
    {
        try
        {
            string fileName = $"{poiId}_{Path.GetFileName(url)}";
            string folder = Path.Combine(FileSystem.AppDataDirectory, $"POI_{poiId}");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string fullPath = Path.Combine(folder, fileName);

            // ALWAYS overwrite
            var bytes = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(fullPath, bytes);

            return fullPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Image download failed: {ex.Message}");
            return null!;
        }
    }


    private bool IsLowStorage()
    {
        var (_, free) = _deviceInfoService.GetStorageInfo();
        double freeGB = free / (1024.0 * 1024 * 1024);
        return freeGB < 3;
    }

    private async Task<string> BuildPoiRequestUrl(long LastUpdated, bool forlang)
    {
        string baseUrl = "https://fuo5mfcpsq355lsytcxxif3usa0xqyih.lambda-url.ap-southeast-1.on.aws";
        string lang = Preferences.Get("App_language", "en");

        string timestamp = DateTime.Now.Ticks.ToString();

        // IF we have 0 POIs, we MUST do a full sync from 0 first to get coordinates
        if (!await _database.HasAnyPoisAsync())
        {
            Console.WriteLine("Database is empty. Performing full initial sync from scratch.");
            return $"{baseUrl}?lastUpdated=0&t={timestamp}";
        }

        if (forlang)
        {
            Console.WriteLine("Specific language refresh requested: " + lang);
            return $"{baseUrl}?lastUpdated=0&lang={lang}&t={timestamp}";
        }

        if (IsLowStorage())
        {
            Console.WriteLine("Device has low storage, requesting data with language filter: " + lang);
            return $"{baseUrl}?lastUpdated=0&lang={lang}&t={timestamp}";
        }
        Console.WriteLine("Device has sufficient storage, requesting incremental data from: " + LastUpdated);
        return $"{baseUrl}?lastUpdated={LastUpdated}&t={timestamp}";
    }

}
public class PoiApiResponse
{

    [JsonPropertyName("updated")]
    public bool Updated { get; set; }

    [JsonPropertyName("newlastupdated")]
    public long LastUpdated { get; set; }

    [JsonPropertyName("pois")]
    public List<Poi> Pois { get; set; } = new List<Poi>();

    [JsonPropertyName("languages")]
    public List<Language_option> Languages { get; set; } = new List<Language_option>();

    [JsonPropertyName("deletedids")]
    public List<string> DeletedIds { get; set; } = new List<string>();

    [JsonPropertyName("descriptions")]
    public List<PoiDescription> Descriptions { get; set; } = new List<PoiDescription>();

    [JsonPropertyName("images")]
    public Dictionary<string, List<string>> Images { get; set; } = new Dictionary<string, List<string>>();

    // Dummy method để báo cho Linker (Trimmer) trong Release mode không được xóa các Setter này
    public void PreserveForTrimmer()
    {
        Updated = true;
        LastUpdated = 0;
        Pois = new List<Poi>();
        Languages = new List<Language_option>();
        DeletedIds = new List<string>();
        Descriptions = new List<PoiDescription>();
        Images = new Dictionary<string, List<string>>();
    }
}