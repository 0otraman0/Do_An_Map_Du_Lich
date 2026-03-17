using SQLite;

namespace MauiAppMain.Models
{
    public class PoiDescription
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [Indexed]
        public string Language { get; set; } = string.Empty;
        [Indexed]
        public int PoiId { get; set; }
    }
}