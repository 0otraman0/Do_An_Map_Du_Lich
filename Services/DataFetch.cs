using MauiAppMain.Models;
using MauiAppMain.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
public class DataFetch
{
    private readonly IDeviceInfoService _deviceInfoService;
    private readonly DatabaseService _database;
    private readonly HttpClient _httpClient;

    public DataFetch(IDeviceInfoService deviceInfoService, DatabaseService databaseService, HttpClient httpClient)
    {
        _deviceInfoService = deviceInfoService;
        _database = databaseService;
        _httpClient = httpClient;
    }
    public async Task FetchData(bool forlang)
    {
        // Build URL with lastSyncTime if EXITSTS
        var url = BuildPoiRequestUrl(Preferences.Get("LastSyncTime", 0L), forlang);
        // SEND REQUEST
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("API request failed");
            return;
        }
        Console.WriteLine("API request suscess");
        if(IsLowStorage())
        {
            Console.WriteLine("Device is low on storage, requesting data with language filter");
        }
         else
        {
            Console.WriteLine("Device has sufficient storage, requesting all data");
        }
        // Read response
        var json = await response.Content.ReadAsStringAsync();
            // Deserialize
            var result = JsonSerializer.Deserialize<PoiApiResponse>(json);
        Console.WriteLine(json);
        // Check if data is updated
        if (result == null || !result.Updated)
        {
            Console.WriteLine("No new data");
            return;
        }
        var prettyJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        Console.WriteLine("new data.");
        Console.WriteLine(prettyJson);

        // 1. DELETE
        if (result.DeletedIds != null)
        {
            foreach (var id in result.DeletedIds)
            {
                await _database.DeletePOIAsync(int.Parse(id));
            }
        }
        // 2. SAVE LANGUAGE OPTIONS
        if (result.Languages !=null)
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
        //  2. SAVE POIs
        if (result.Pois != null)
        {
            await _database.SavePoisAsync(result.Pois);
        }
        var pois = await _database.GetAllPoisAsync();
        foreach (var item in pois)
        {
            Console.WriteLine("POI: " + item.Latitude + " - Id: " + item.Id );
        }
        //  3. SAVE DESCRIPTIONS
        if (result.Descriptions != null)
        {
            await _database.SaveDescriptionsAsync(result.Descriptions);
        }
        var des = await _database.GetAllDescriptionsAsync();
        foreach (var item in des)
        {
            Console.WriteLine("Description: " + item.Name + " - Id: " + item.PoiId);
        }

        //  4. HANDLE IMAGES
        bool isLowStorage = IsLowStorage();

        if (result.Images != null)
        {
            foreach (var kvp in result.Images)
            {
                int poiId = int.Parse(kvp.Key);
                var urls = kvp.Value;
                if (urls == null || urls.Count == 0)
                    continue;
                await _database.DeleteImagesByPoiIdAsync(poiId);

                // LOAD ẢNH MỚi

                    foreach (var urlItem in urls)
                    {
                        if (urlItem.EndsWith("/")) continue;
                        if (string.IsNullOrWhiteSpace(urlItem)) continue;

                        //  Download and store local path
                        var localPath = await DownloadImageAsync(poiId, urlItem);

                        Console.WriteLine("Processing image URL for POI " + poiId + ": " + urlItem);

                        if (!string.IsNullOrEmpty(localPath))
                        {
                            await _database.AddImageAsync(poiId, localPath); //  SAVE LOCAL PATH
                        }
                }
            }
        }

        var ImagePaths = await _database.GetAllImagesAsync();
        foreach (var img in ImagePaths)
        {
            Console.WriteLine($" -> {img.Url}" );
        }        //  5. UPDATE LAST SYNC
        Preferences.Set("LastSyncTime", result.LastUpdated);
        Console.WriteLine("Last update: " + Preferences.Get("LastSyncTime", 12L));
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

    private string BuildPoiRequestUrl(long LastUpdated, bool forlang)
    {
        string baseUrl = "https://fuo5mfcpsq355lsytcxxif3usa0xqyih.lambda-url.ap-southeast-1.on.aws";

        if (IsLowStorage())
        {
            string lang = Preferences.Get("App_language", "en");
            if(forlang == true)
            {
                Console.WriteLine("Language changed, requesting data with language filter: " + lang);
                return $"{baseUrl}?lastUpdated={LastUpdated}&lang={lang}";
            }
            Console.WriteLine("Device has low storage, requesting data with language filter: " + lang);
            return $"{baseUrl}?lastUpdated=0&lang={lang}";
        }
        Console.WriteLine("Device has sufficient storage, requesting all data");
        return $"{baseUrl}?lastUpdated={LastUpdated}";
    }
}
public class PoiApiResponse
{

    [JsonPropertyName("updated")]
    public bool Updated { get; set; }

    [JsonPropertyName("newlastupdated")]
    public long LastUpdated { get; set; }

    [JsonPropertyName("pois")]
    public List<Poi> Pois { get; set; } = new();

    [JsonPropertyName("languages")]
    public List<Language_option> Languages { get; set; } = new();

    [JsonPropertyName("deletedids")]
    public List<string> DeletedIds { get; set; } = null!;

    [JsonPropertyName("descriptions")]
    public List<PoiDescription> Descriptions { get; set; } = null!;

    [JsonPropertyName("images")]
    public Dictionary<string, List<string>> Images { get; set; } = null!;
}