using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.Service
{
    public class AppSettings
    {
        public string Secret { get; set; }
        public string AdminUserName { get; set; }
        public string AdminPassword { get; set; }
        public bool Auth { get; set; }
    }
}