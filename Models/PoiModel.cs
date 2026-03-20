using SQLite;

namespace MauiAppMain.Models
{
    public class Poi
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;
        public double RadiusMeters { get; set; } = 0;
        public bool IsTriggered { get; set; } = false;
        public string ImageUrlsJson { get; set; }
        public string isFavorite { get; set; } = "false";


        [Ignore] // Không lưu cột này vào DB, chỉ dùng để hiển thị
        public List<string> ImageList => string.IsNullOrEmpty(ImageUrlsJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(ImageUrlsJson);
    }
}
