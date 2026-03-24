
namespace MauiAppMain.Models
{
    public class PoiJson
    {
        public int Id { get; set; } = 0;
        public string Name_vi { get; set; } = string.Empty;
        public string Name_en { get; set; } = string.Empty;
        public string Name_ja { get; set; } = string.Empty;

        public string Description_vi { get; set; } = string.Empty;
        public string Description_en { get; set; } = string.Empty;
        public string Description_ja { get; set; } = String.Empty;

        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;

        public double Radius { get; set; } = double.NaN;

        public string ImageUrl { get; set; } = string.Empty;
    }
}
