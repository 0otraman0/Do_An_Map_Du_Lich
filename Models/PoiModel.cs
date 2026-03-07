using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MauiAppMain.Models
{
    public class PointOfInterest
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;
        public double RadiusMeters { get; set; } = 0;
        public bool IsTriggered { get; set; } = false;
        public string ImageUrlsJson { get; set; }

        [Ignore] // Không lưu cột này vào DB, chỉ dùng để hiển thị
        public List<string> ImageList => string.IsNullOrEmpty(ImageUrlsJson)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(ImageUrlsJson);
    }
}
