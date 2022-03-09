using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosFeedTestsConsole
{
    internal class GeoTag
    {
        private static readonly Random _random = new Random();
        
        public double Longitude { get; set; } = _random.NextDouble() * 360-180;

        public double Latitude { get; set; } = _random.NextDouble() * 90;
    }
}