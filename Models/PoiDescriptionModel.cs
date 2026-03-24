using SQLite;
using System.Reflection.Metadata;
using System.Text.Json.Serialization;

namespace MauiAppMain.Models
{
    public class PoiDescription
    {
        [PrimaryKey]

        [JsonPropertyName("Id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Description")]
        public string Description { get; set; } = string.Empty;

        [Indexed]
        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;

        [Indexed]
        [JsonPropertyName("PoiId")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]

        public int PoiId { get; set; }

        [JsonPropertyName("Address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("Content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("Detail")]
        public string Detail { get; set; } = string.Empty;
    }
}