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
            {
                return;
            }
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

        public async Task<List<PointOfInterest>> GetPOIsAsync()
        {
            await Init();
            var lang = Preferences.Get("App_language", "en");

            // 1. Lấy toàn bộ dữ liệu từ các bảng chính về RAM một lần duy nhất
            var allPois = await _database!.Table<Poi>().ToListAsync();
            var allTranslations = await _database.Table<PoiDescription>().Where(t => t.Language == lang).ToListAsync();
            //var allImages = await _database.Table<POIImage>().ToListAsync();

            // 2. Sử dụng LINQ để map dữ liệu trên RAM (cực nhanh)
            var result = allPois.Select(p =>
            {
                var t = allTranslations.FirstOrDefault(x => x.PoiId == p.Id);
                //var imageUrls = allImages.Where(i => i.POIId == p.Id).Select(i => i.Url).ToList();

                return new PointOfInterest
                {
                    Id = p.Id,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Name = t?.Name ?? "N/A",
                    Description = t?.Description ?? "",
                    //ImageUrlsJson = JsonSerializer.Serialize(imageUrls),
                    IsFavorite = p.IsFavorite,
                    priorityLevel = p.priorityLevel,
                    Address = t?.Address ?? ""
                };
            })
            .OrderByDescending(p => p.priorityLevel)
            .ToList();

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
        public async Task UpdatePOIAsync(PointOfInterest poi)
        {
            await Init();

            var dbPoi = await _database.Table<Poi>()
                .FirstOrDefaultAsync(p => p.Id == poi.Id);

            if (dbPoi != null)
            {
                dbPoi.IsFavorite = poi.IsFavorite; // IMPORTANT
                await _database.UpdateAsync(dbPoi);
            }
        }
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

        //hàm search
        public async Task<List<PointOfInterest>> SearchPoiAsync(string keyword)
        {
            await Init();

            var lang = Preferences.Get("App_language", "en");

            // 🔥 Chỉ tìm theo NAME
            var descriptions = await _database!.Table<PoiDescription>()
                .Where(d => d.Language == lang &&
                            d.Name.Contains(keyword))
                .ToListAsync();

            var pois = await _database.Table<Poi>().ToListAsync();

            var result = (from d in descriptions
                          join p in pois on d.PoiId equals p.Id
                          select new PointOfInterest
                          {
                              Id = p.Id,
                              Latitude = p.Latitude,
                              Longitude = p.Longitude,
                              Name = d.Name,
                              Description = d.Description, // vẫn hiển thị, chỉ không search thôi
                              IsFavorite = p.IsFavorite,
                              priorityLevel = p.priorityLevel,
                              Address = d.Address,
                              ImageUrlsJson = ""
                          })
                          .ToList();

            return result;
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

        // Hàm này dùng để cập nhật trạng thái yêu thích CỰC NHANH
        public async Task UpdateFavoriteStatusAsync(PointOfInterest poi)
        {
            await Init();

            // Dùng SQL trực tiếp để loại bỏ mọi nghi ngờ về việc mapping Object
            // SQLite lưu true/false là 1/0
            string sql = "UPDATE Poi SET IsFavorite = ? WHERE Id = ?";
            int val = poi.IsFavorite ? 1 : 0;

            try
            {
                var rows = await _database!.ExecuteAsync(sql, val, poi.Id);
                System.Diagnostics.Debug.WriteLine($"[DB] Update Id {poi.Id} to {val}. Rows affected: {rows}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] {ex.Message}");
            }
        }


        // hàm clear database để ở cuối cùng
        public async Task ClearAllDataAsync()
        {
            await Init();

            // 🔥 Xóa dữ liệu trong DB
            await _database.DeleteAllAsync<POIImage>();
            await _database.DeleteAllAsync<PoiDescription>();
            await _database.DeleteAllAsync<Language_option>();
            await _database.DeleteAllAsync<Poi>();
        }
    }
}