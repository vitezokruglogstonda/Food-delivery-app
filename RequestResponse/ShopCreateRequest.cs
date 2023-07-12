using Microsoft.AspNetCore.Http;
using MongoDB.Driver.GeoJsonObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.RequestResponse
{
    public class ShopCreateRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public IFormFile? Picture { get; set; }
        public string Tags { get; set; }
        //owner
        public string UserName { get; set; }
        public string Password { get; set; }

        public double Longitude { get; set; }
        public double Latitude { get; set; }
        //public LocationCoord LocationCoordinates { get; set; }
    }
}
