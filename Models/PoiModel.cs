using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiAppMain.Models
{
    public class PointOfInterest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; } = 0;
        public double Longitude { get; set; } = 0;
        public double RadiusMeters { get; set; } = 0;
        public bool IsTriggered { get; set; } = false;
    }
}
