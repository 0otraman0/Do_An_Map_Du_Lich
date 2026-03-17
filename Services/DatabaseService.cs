using MauiAppMain.Models;
using SQLite;

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

            var pois = await _database.Table<PointOfInterest>().ToListAsync();
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
                    Description = t?.Description ?? ""
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
    }
}