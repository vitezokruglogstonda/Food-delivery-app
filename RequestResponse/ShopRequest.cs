using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.RequestResponse
{
    public class ShopRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public IFormFile? Picture { get; set; }
        public string Tags { get; set; }
        public LocationCoord LocationCoordinates { get; set; }
    }
}
