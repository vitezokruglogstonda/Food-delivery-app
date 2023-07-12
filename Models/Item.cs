using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.Models
{
    public class Item
    {

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ID { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Picture { get; set; }
        public double Price { get; set; }
        public int Amount { get; set; }

        //public MongoDBRef Shop { get; set; }
        //public Shop ShopRef { get; set; }

        public List<string> Tags { get; set; }

        public Item()
        {
            ID = ObjectId.GenerateNewId().ToString();
        }
        //public Item() { }

        //public Item(Item old, int amount)
        //{
        //    this.Name = old.Name;
        //    this.Description = old.Description;
        //    this.Picture = old.Picture;
        //    this.Price = old.Price;
        //    this.Amount = amount;
        //    this.Shop = old.Shop;
        //    if (old.Tags.Any())
        //    {
        //        this.Tags = new List<string>(old.Tags);
        //    }
        //}
    }
}
