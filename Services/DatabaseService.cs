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

        public Task<int> UpdatePOIAsync(PointOfInterest poi)
        {
            return _database.UpdateAsync(poi);
        }
    }
}
