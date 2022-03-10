using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CosmosFeedTestsConsole
{
    internal class DocumentBatch
    {
        //[JsonProperty(PropertyName = "_count")]
        public int _count { get; set; }
    }
}