using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace MauiAppMain.Models
{
    public class Language_option
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
