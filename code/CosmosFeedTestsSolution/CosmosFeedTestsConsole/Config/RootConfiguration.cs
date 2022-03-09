using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosFeedTestsConsole.Config
{
    internal class RootConfiguration
    {
        public string? Endpoint { get; set; }
        
        public string? AccessKey { get; set; }
        
        public string? Database { get; set; }
        
        public string? Container { get; set; }
    }
}