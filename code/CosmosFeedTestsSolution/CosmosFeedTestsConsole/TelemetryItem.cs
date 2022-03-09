using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosFeedTestsConsole
{
    internal class TelemetryItem
    {
        private static readonly Random _random = new Random();

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; } = GenerateRandomString(32);

        public string Part { get; set; } = GenerateRandomString(5);

        public string DeviceId { get; set; } = GenerateRandomString(16);

        public double Temperature { get; set; } = 20 + _random.NextDouble() * 10;

        public double[] Metrics { get; set; } = new[]
        {
            1000 + _random.NextDouble() * 200,
            1000 + _random.NextDouble() * 200,
            1000 + _random.NextDouble() * 200,
            1000 + _random.NextDouble() * 200,
            1000 + _random.NextDouble() * 200
        };

        public GeoTag GetTag { get; set; } = new GeoTag();

        public object? NullSubObject { get; set; } = null;

        public bool IsActive { get; set; } = _random.Next(1) == 0;

        public double ForceInNewton { get; set; } = 200 + _random.NextDouble() * 1000;

        private static string GenerateRandomString(int length)
        {
            var characters = Enumerable.Range(0, length)
                .Select(i => (char)(_random.Next(26) + 'A'))
                .ToArray();
            var text = new string(characters);

            return text;
        }
    }
}