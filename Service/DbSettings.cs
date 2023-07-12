using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.Service
{
    public class DbSettings
    {
        public string DatabaseName { get; set; }
        public string ConnectionString { get; set; }
        public string ShopCollectionName { get; set; }
        public string OrderCollectionName { get; set; }
        public string UserCollectionName { get; set; }
    }
}
