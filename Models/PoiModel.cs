using SQLite;
using System.Text.Json.Serialization;

namespace MauiAppMain.Models
{
    public class Poi
    {
        [PrimaryKey]
        [JsonPropertyName("PoiId")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]

        public int Id { get; set; }
        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;
        [JsonPropertyName("Radius")]
        public double RadiusMeters { get; set; } = 0;
        public bool IsTriggered { get; set; } = false;
        public string ImageUrlsJson { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsDeleted { get; set; }
        [JsonPropertyName("Priority")]
        public int priorityLevel { get; set; } = 0;

        [Ignore] // Không lưu cột này vào DB, chỉ dùng để hiển thị
        public List<string> ImageList => string.IsNullOrEmpty(ImageUrlsJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(ImageUrlsJson);
    }
}
