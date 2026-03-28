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
        public async Task<List<Poi>> GetAllPoisAsync()
        {
            await Init();
            return await _database!.Table<Poi>().ToListAsync();
        }
        public async Task<List<PoiDescription>> GetAllDescriptionsAsync()
        {
            await Init();
            return await _database!.Table<PoiDescription>().ToListAsync();
        }
        //public async Task<List<PointOfInterest>> GetPOIsAsync()
        //{
        //    await Init();
        //    var lang = Preferences.Get("App_language", "en");
        //    Console.WriteLine("Current language: " + lang);
        //    var pois = await _database!.Table<Poi>().ToListAsync();
        //    var translations = await _database.Table<PoiDescription>()
        //        .Where(t => t.Language == lang)
        //        .ToListAsync();

        //    var result = pois.Select(p =>
        //    {
        //        var t = translations.FirstOrDefault(x => x.PoiId == p.Id);

        //        var imageList = _database.Table<POIImage>()
        //        .Where(i => i.POIId == p.Id).ToListasync();

        //        System.Text.Json.JsonSerializer.Serialize(imageList);

        //        return new PointOfInterest
        //        {
        //            Id = p.Id,
        //            Latitude = p.Latitude,
        //            Longitude = p.Longitude,
        //            Name = t?.Name ?? "N/A",
        //            Description = t?.Description ?? "",
        //            ImageUrlsJson = p.ImageUrlsJson,
        //        };
        //    }).ToList();
        //    return result;
        //}

        public async Task<List<PointOfInterest>> GetPOIsAsync()
        {
            await Init();

            var lang = Preferences.Get("App_language", "en");
            Console.WriteLine("Current language: " + lang);

            var pois = await _database!.Table<Poi>().ToListAsync();
            var translations = await _database.Table<PoiDescription>()
                .Where(t => t.Language == lang)
                .ToListAsync();

            var result = new List<PointOfInterest>();
            foreach (var p in pois)
            {
                var t = translations.FirstOrDefault(x => x.PoiId == p.Id);

                var imageList = await _database.Table<POIImage>()
                .Where(i => i.POIId == p.Id)
                .ToListAsync();

                var imageUrls = imageList.Select(i => i.Url).ToList();

                var imageJson = JsonSerializer.Serialize(imageUrls);

                result.Add(new PointOfInterest
                {
                    Id = p.Id,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Name = t?.Name ?? "N/A",
                    Description = t?.Description ?? "",
                    ImageUrlsJson = imageJson
                });
            }

            return result;
        }

        public async Task SavePoisAsync(List<Poi> pois)
        {
            await Init();

            if (_database is null)
                return;

            if (pois == null || pois.Count == 0)
                return;

            foreach (var poi in pois)
            {
                if (poi == null) continue;

                var existing = await _database!.Table<Poi>()
                                             .Where(p => p.Id == poi.Id)
                                             .FirstOrDefaultAsync();

                if (existing != null)
                {
                    // UPDATE
                    await _database.UpdateAsync(poi);
                }
                else
                {
                    // INSERT
                    await _database.InsertAsync(poi);
                }
            }
        }
        public async Task AddPOIAsync(Poi poi)
        {
            await Init();
            await _database!.InsertAsync(poi);
        }
        public async Task UpdatePoiAsync(Poi poi)
        {
            await Init(); await _database!.UpdateAsync(poi);
        }
        //public async Task List<<Poiimages>> GetAllImageAsync()
        //{
        //    await Init();
        //    return await _database!.Table<Poi>().ToListAsync();
        //}
        public async Task DeletePOIAsync(int id)
        {
            await Init();

            // Delete descriptions
            var descriptions = await _database!.Table<PoiDescription>()
                .Where(d => d.PoiId == id)
                .ToListAsync();

            foreach (var desc in descriptions)
            {
                await _database.DeleteAsync(desc);
            }
            //  DELETE IMAGES
            var images = await _database.Table<POIImage>()
                .Where(i => i.POIId == id)
                .ToListAsync();

            foreach (var img in images)
            {
                await _database.DeleteAsync(img);
                if (File.Exists(img.Url))
                {
                    File.Delete(img.Url);
                }
            }

            // Delete POI
            var pois = await _database.Table<Poi>()
                .Where(p => p.Id == id)
                .ToListAsync();

            foreach (var poi in pois)
            {
                await _database.DeleteAsync(poi);
            }
        }
        public async Task SaveDescriptionsAsync(List<PoiDescription> descriptions)
        {
            await Init();

            Console.WriteLine("Saving descriptions: " + descriptions.Count);
            foreach (var desc in descriptions)
            {
                Console.WriteLine("Saving descriptions: " + desc.Address + desc.Language + desc.Detail);
                var existing = await _database!.Table<PoiDescription>()
                .Where(d => d.PoiId == desc.PoiId && d.Language == desc.Language)
                .FirstOrDefaultAsync();
                Console.WriteLine("Existing description for POIId " + desc.PoiId + ": " + (existing != null ? existing.Name : "null"));
                if (existing != null)
                {
                    // UPDATE
                    await _database.UpdateAsync(desc);
                }
                else
                {
                    // INSERT
                    await _database.InsertAsync(desc);
                }
            }
        }
        //public async Task AddPOIDescriptionAsync(PoiDescription poides)
        //{
        //    await Init();
        //    await _database!.InsertAsync(poides);
        //}

        //public async Task AddTranslationAsync(PoiDescription translation)
        //{
        //    await Init();
        //    await _database!.InsertAsync(translation);
        //}
        //public async Task<List<PoiDescription>> GetTranslationsAsync(string languageCode)
        //{
        //    await Init();
        //    return await _database!.Table<PoiDescription>()
        //        .Where(t => t.Language == languageCode)
        //        .ToListAsync();
        //}

        //hàm thêm ảnh
        public async Task AddImageAsync(int poiId, string urlItem)
        {
            await Init();

            var existing = await _database!.Table<POIImage>()
                .Where(i => i.POIId == poiId && i.Url == urlItem)
                .FirstOrDefaultAsync();

            if (existing != null)
                return;

            POIImage image = new POIImage()
            {
                POIId = poiId,
                Url = urlItem,
                ImageName = Path.GetFileName(urlItem)
            };

            await _database.InsertAsync(image);
        }
        //ham them language 
        public async Task AddLanguageAsync(Language_option language)
        {
            await Init();

                var existing = await _database!.Table<Language_option>()
                                             .Where(p => p.Code == language.Code)
                                             .FirstOrDefaultAsync();
                if (existing != null)
                {
                    // UPDATE
                    await _database.UpdateAsync(language);
                }
                else
                {
                    // INSERT
                    await _database.InsertAsync(language);
                }
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
        public async Task<bool> IsLanguageDataAvailable(string code)
        {
            await Init();

            var count = await _database!.Table<PoiDescription>()
                .Where(d => d.Language == code)
                .CountAsync();

            return count > 0;
        }
    }
}