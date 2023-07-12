using MasterFood.Service;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.Models
{
    public class User 
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ID { get; set; }
        //public string UserType { get; set; }
        public IUserService.AccountType UserType { get; set; }
        public string UserName { get; set; }
        public byte[] Password { get; set; }
        public byte[] Salt { get; set; }
        [BsonElement("online")]
        public bool Online { get; set; }
        public MongoDBRef? Shop { get; set; }
    }
}
