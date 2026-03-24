using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MauiAppMain.Models
{
    public class Language_option
    {
        [PrimaryKey]
        [JsonPropertyName("Language_Code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("Language_Name")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("Full_Language_Code")]
        public string Full_Language_Code { get; set; } = string.Empty;
    }
}