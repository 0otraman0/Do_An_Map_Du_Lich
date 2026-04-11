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

            var pois = await _database!.Table<Poi>().ToListAsync();
            var translations = await _database.Table<PoiDescription>()
                .Where(t => t.Language == lang)
                .ToListAsync();
                
            // LOAD TOÀN BỘ ẢNH LÊN RAM ĐỂ TRÁNH LỖI N+1 SQL QUERIES (Nguyên nhân chậm 5-6s)
            var allImages = await _database.Table<POIImage>().ToListAsync();

            var result = new List<PointOfInterest>();
            foreach (var p in pois)
            {
                var t = allTranslations.FirstOrDefault(x => x.PoiId == p.Id);
                //var imageUrls = allImages.Where(i => i.POIId == p.Id).Select(i => i.Url).ToList();

                // Truy xuất ảnh từ trên RAM thay vì gọi DB cho từng POI
                var imageList = allImages.Where(i => i.POIId == p.Id).ToList();

                // ONLY TAKE LOCAL FILES
                var imagePaths = imageList
                .Select(i => i.Url)
                .ToList();


                var imageJson = JsonSerializer.Serialize(imagePaths);
      
                result.Add(new PointOfInterest
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
        public async Task AddImageAsync(int poiId, string path)
        {
            try
            {
                await Init();

                var fileName = Path.GetFileName(path);
                Console.WriteLine($"FileName: {fileName}");

                var existing = await _database!.Table<POIImage>()
                    .Where(i => i.POIId == poiId && i.ImageName == fileName)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    existing.Url = path;
                    await _database.UpdateAsync(existing);
                    return;
                }

                POIImage image = new POIImage()
                {
                    POIId = poiId,
                    Url = path,
                    ImageName = fileName
                };

                await _database.InsertAsync(image);

                Console.WriteLine("Insert done");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AddImageAsync ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
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

        public async Task<List<POIImage>> GetAllImagesAsync()
        {
            await Init();
            return await _database!.Table<POIImage>().ToListAsync();
            
        }

        public async Task DeleteImagesByPoiIdAsync(int poiId)
        {
            await Init();

            var images = await _database!.Table<POIImage>()
                .Where(i => i.POIId == poiId)
                .ToListAsync();

            foreach (var img in images)
            {
                // 🔥 delete file local nếu tồn tại
                if (File.Exists(img.Url))
                {
                    try
                    {
                        File.Delete(img.Url);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Delete file error: " + ex.Message);
                    }
                }

                // 🔥 delete DB
                await _database.DeleteAsync(img);
            }

            Console.WriteLine($"Deleted all images for POI {poiId}");
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