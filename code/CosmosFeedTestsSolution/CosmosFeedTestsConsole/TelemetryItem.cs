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
        
        public string DeviceId { get; set; } = GenerateRandomString(16);
        
        public string Part { get; set; } = GenerateRandomString(5);

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