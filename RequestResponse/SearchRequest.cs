using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.RequestResponse
{
    public class SearchRequest
    {
        public string? Name { get; set; }
        public List<string>? Tags { get; set; }

        
    }
}
