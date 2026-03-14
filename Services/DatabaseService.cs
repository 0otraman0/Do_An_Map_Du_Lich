using MauiAppMain.Models;
using SQLite;

namespace MauiAppMain.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        public async Task Init()
        {
            if (_database != null)
                return;

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "poi.db");

            _database = new SQLiteAsyncConnection(dbPath);

            await _database.CreateTableAsync<PointOfInterest>();
            await _database.CreateTableAsync<POIImage>();
            await _database.CreateTableAsync<Language_option>();
        }
            
        public async Task<List<PointOfInterest>> GetPOIsAsync()
        {
            await Init();
            return await _database.Table<PointOfInterest>().ToListAsync();
        }

        public async Task AddPOIAsync(PointOfInterest poi)
        {
            await Init();
            await _database.InsertAsync(poi);
        }

        //hàm thêm ảnh
        public async Task AddImageAsync(POIImage image)
        {
            await Init();
            await _database.InsertAsync(image);
        }
        //ham them language 
        public async Task AddLanguageAsync(Language_option language)
        {
            await Init();
            await _database.InsertAsync(language);
        }
        // Ham lấy tất cả ngôn ngữ
        public async Task<List<Language_option>> GetLanguagesAsync()
        {
            await Init();
            return await _database.Table<Language_option>().ToListAsync();
        }
        public async Task<List<PointOfInterest>> SearchPoiAsync(string keyword)
        {
            keyword = keyword.ToLower();

            return await _database.Table<PointOfInterest>()
                .Where(p => p.Name.ToLower().Contains(keyword) || p.Description.ToLower().Contains(keyword))
                // limit suggestions use .take(5) if you want to limit the number of suggestions
                .ToListAsync();
        }
        public async Task UpdateLanguageAsync(Language_option language)
        {
            await Init();
            await _database.UpdateAsync(language);
        }
    }
}