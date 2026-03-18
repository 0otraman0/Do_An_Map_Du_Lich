using MauiAppMain.Models;
using SQLite;
using System.Text.Json;

namespace MauiAppMain.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;

        public async Task Init()
        {
            if (_database != null)
                return;

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "poi.db");

            _database = new SQLiteAsyncConnection(dbPath);

            await _database.CreateTableAsync<Poi>();
            await _database.CreateTableAsync<POIImage>();
            await _database.CreateTableAsync<Language_option>();
            await _database.CreateTableAsync<PoiDescription>();
        }

        public async Task<List<PointOfInterest>> GetPOIsAsync()
        {
            await Init();
            var lang = Preferences.Get("App_language", "en");

            var pois = await _database.Table<Poi>().ToListAsync();
            var translations = await _database.Table<PoiDescription>()
                .Where(t => t.Language == lang)
                .ToListAsync();

            var result = pois.Select(p =>
            {
                var t = translations.FirstOrDefault(x => x.PoiId == p.Id);

                return new PointOfInterest
                {
                    Id = p.Id,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Name = t?.Name ?? "N/A",
                    Description = t?.Description ?? "",
                    ImageUrlsJson = p.ImageUrlsJson,
                };
            }).ToList();
            return result;
        }

        public async Task AddPOIAsync(Poi poi)
        {
            await Init();
            await _database!.InsertAsync(poi);
        }

        public async Task AddPOIDescriptionAsync(PoiDescription poides)
        {
            await Init();
            await _database!.InsertAsync(poides);
        }

        public async Task AddTranslationAsync(PoiDescription translation)
        {
            await Init();
            await _database!.InsertAsync(translation);
        }

        public async Task<List<PoiDescription>> GetTranslationsAsync(string languageCode)
        {
            await Init();
            return await _database!.Table<PoiDescription>()
                .Where(t => t.Language == languageCode)
                .ToListAsync();
        }

        //hàm thêm ảnh
        public async Task AddImageAsync(POIImage image)
        {
            await Init();
            await _database!.InsertAsync(image);
        }
        //ham them language 
        public async Task AddLanguageAsync(Language_option language)
        {
            await Init();
            await _database!.InsertAsync(language);
        }
        // Ham lấy tất cả ngôn ngữ
        public async Task<List<Language_option>> GetLanguagesAsync()
        {
            await Init();
            return await _database!.Table<Language_option>().ToListAsync();
        }
        public async Task<List<PointOfInterest>> SearchPoiAsync(string keyword)
        {
            keyword = keyword.ToLower();

            return await _database!.Table<PointOfInterest>()
                .Where(p => p.Name.ToLower().Contains(keyword) || p.Description.ToLower().Contains(keyword))
                // limit suggestions use .take(5) if you want to limit the number of suggestions
                .ToListAsync();
        }
        public async Task UpdateLanguageAsync(Language_option language)
        {
            await Init();
            await _database!.UpdateAsync(language);
        }

        public async Task SeedData()
        {
            // kiểm tra dữ liệu POI đã tồn tại chưa, nếu chưa thì thêm vào
             await Init();
            var existing = await _database.Table<Poi>().CountAsync();
            if (existing == 0)
            {
                await _database.DeleteAllAsync<Poi>();
                await _database.DeleteAllAsync<PoiDescription>();


                await _database.CreateTableAsync<Poi>();
                await _database.CreateTableAsync<PoiDescription>();

                var stream = await FileSystem.OpenAppPackageFileAsync("poi.json");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                var poiList = JsonSerializer.Deserialize<List<PoiJson>>(json);

                var imageList1 = new List<string> { "school_1.jpg", "school_2.jpg", "school_3.jpg" };
                var imageList2 = new List<string> { "cafe_1.jpg", "cafe_2.jpg" };
                // thêm dữ liệu POI
                foreach (var item in poiList)
                {
                    Poi poi = new Poi()
                    {
                        Latitude = item.Latitude,
                        Longitude = item.Longitude,
                        RadiusMeters = item.Radius,
                        ImageUrlsJson = JsonSerializer.Serialize(imageList1)
                    };
                    await AddPOIAsync(poi);
                    //await _database.InsertAsync(poi);
                    int newId = poi.Id;

                    var poides = new List<PoiDescription>{
                        new PoiDescription()
                        {
                            PoiId = newId,
                            Name = item.Name_en,
                            Description = item.Description_en,
                            Language = "en"
                        },
                        new PoiDescription()
                        {
                            PoiId = newId,
                            Name = item.Name_vi,
                            Description = item.Description_vi,
                            Language = "vi"
                        },
                        new PoiDescription()
                        {
                            PoiId = newId,
                            Name=item.Name_ja,
                            Description = item.Description_ja,
                            Language = "ja"
                        }
                    };
                    foreach (var i in poides)
                        await AddPOIDescriptionAsync(i);
                }
            }
            // kiểm tra dữ liệu ngôn ngữ đã tồn tại chưa, nếu chưa thì thêm vào
            var existing_lang = await GetLanguagesAsync();
            if (existing_lang.Count == 0)
            {
                var initialLanguage = new List<Language_option>
                {
                    new Language_option
                    {
                        Code = "en",
                        Language = "English"
                    },
                    new Language_option
                    {
                        Code = "vi",
                        Language = "Tiếng Việt"
                    },
                    new Language_option
                    {
                        Code = "ja",
                        Language = "日本語"
                    }
                };
                foreach (var lang in initialLanguage) await AddLanguageAsync(lang);
            }
        }

    }
}