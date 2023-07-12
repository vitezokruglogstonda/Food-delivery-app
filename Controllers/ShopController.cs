using MasterFood.Models;
using MasterFood.RequestResponse;
using MasterFood.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MongoDB.Driver.GeoJsonObjectModel;
using MasterFood.Authentication;

namespace MasterFood.Controllers
{
    public class ShopController : Controller
    {
        public IUserService Service { get; set; }
       // public IWebHostEnvironment Environment { get; set; }
        public AppSettings _appSettings { get; set; }

        private readonly IMongoCollection<Shop> Shops;
        private readonly IMongoCollection<OrderList> OrderLists;
        private readonly IMongoCollection<User> Users;
        private readonly IMongoDatabase database;
        private readonly IOptions<DbSettings> dbSettings;

        public ShopController(IUserService service, IOptions<DbSettings> dbSettings, IOptions<AppSettings> appsettings)
        {
            this.dbSettings = dbSettings;
            //this.Environment = environment;
            this._appSettings = appsettings.Value;
            MongoClient client = new MongoClient(dbSettings.Value.ConnectionString);
            database = client.GetDatabase(dbSettings.Value.DatabaseName);
            this.Shops = database.GetCollection<Shop>(dbSettings.Value.ShopCollectionName);
            this.OrderLists = database.GetCollection<OrderList>(dbSettings.Value.OrderCollectionName);
            this.Users = database.GetCollection<User>(dbSettings.Value.UserCollectionName);
            this.Service = service;
        }

        #region Shop methods

        [HttpGet]
        [Route("Shop")] //return all shops
        public async Task<IActionResult> GetShops() 
        {
            var shops = await Shops.Find(new BsonDocument()).ToListAsync();
            return Ok(shops);
        }

        [HttpGet]
        [Route("Shop/Popular")] //return popular 6
        public async Task<IActionResult> GetPopularShops() { 
            var orderLists = await this.OrderLists.Aggregate()
                .Project(ol => new {
                    ol.ID,
                    OrderCount = (ol.Active != null? ol.Active.Count : 0) + 
                                 (ol.History != null? ol.History.Count : 0)
                })
                .SortByDescending(ol => ol.OrderCount)
                .Limit(6)
                .ToListAsync();
            var shopIDs = orderLists.Select(ol => ol.ID);
            var filter = Builders<Shop>.Filter.In("ID", shopIDs);
            var shops = await Shops.Find(filter).ToListAsync();
            return Ok(shops); 
        }

        [HttpGet]
        [Route("Shop/{id}")]
        public async Task<IActionResult> GetShopByID(string id) 
        {
            Shop shop = this.Service.GetShop(id);
            return Ok(shop);
        }

        //[Auth]
        [HttpPut]
        [Route("Shop/{id}")]
        public async Task<IActionResult> UpdateShop(string id, [FromForm] ShopRequest changes)  // TODO fix fromform
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsOwner(username, id)) return BadRequest(new { message = "User does not own shop." });
 

            Shop shop = this.Service.GetShop(id);
            if (changes.Name != null)
            {
                shop.Name = changes.Name;
            }
            if (changes.Description != null)
            {
                shop.Description = changes.Description;
            }
            if (changes.Picture != null)
            {
                this.Service.DeleteImage(shop.Picture, IUserService.ImageType.Shop);
                shop.Picture = this.Service.AddImage(changes.Picture, IUserService.ImageType.Shop);
            }
            if (changes.Tags != null)
            {
                string[] tagArray = changes.Tags.Split(',');
                var tagList = tagArray.ToList<string>();
                shop.Tags = tagList;
            }

            this.Service.UpdateShop(shop);
            return Ok(); 
        }

        [HttpPost]
        [Route("Shop")]
        public async Task<IActionResult> CreateShop([FromForm] ShopCreateRequest data) 
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsAdmin(username)) return BadRequest(new { message = "User is not Admin." });

            //create owner
            User user = this.Service.GetUser(null, data.UserName);
            if (user != null)
            {
                return BadRequest(new { message = "User already exists." });
            }

            byte[] password, salt;
            this.Service.CreatePassword(out password, out salt, data.Password);
            user = new User
            {
                UserName = data.UserName,
                Password = password,
                Salt = salt,
                Online = false,
                Shop = null,
               
    };
            this.Service.CreateUser(user);

            //create shop
            User userr = this.Service.GetUser(null, data.UserName);
            if (user.Shop != null)
            {
                return BadRequest(new { message = "Korisnik vec ima prodavnicu." });
            }

            string img_path;
            if (data.Picture != null)
            {
                img_path = this.Service.AddImage(data.Picture, IUserService.ImageType.Shop);
            }
            else
            {
                img_path = "default.png";
            }

            
            List<string> tagList = new List<string>(); 
            if (data.Tags != null)
                tagList = data.Tags.Split(',').ToList<string>();

            Shop shop = new Shop
            {
                Name = data.Name,
                Description = data.Description,
                Picture = img_path,
                Tags = tagList,
                Owner = new MongoDBRef("User", BsonValue.Create(user.ID)),
                Items = null,
                Location = new GeoJsonPoint<GeoJson2DCoordinates>(new GeoJson2DCoordinates(data.Longitude, data.Latitude))
            //Location = new GeoJsonPoint(GeoJson.Position(data.LocationCoordinates.Longtitude, data.LocationCoordinates.Longtitude))
        };

            //store shop
            var userFilter = Builders<User>.Filter.Eq("ID", userr.ID);
            this.Shops.InsertOne(shop);
            user.Shop = new MongoDBRef("Shop", BsonValue.Create(shop.ID));
            //user.Shop = new MongoDBRef("Shop", BsonValue.Create(shop.ID));
            this.Users.ReplaceOne(userFilter, user);
            return Ok(new { OwnerID = user.ID, ShopID = shop.ID });
        }

        [HttpDelete]
        [Route("Shop/{id}")]
        public async Task<IActionResult> DeleteShop(string id)
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsOwner(username, id)) return BadRequest(new { message = "User does not own shop." });


            var filterS = Builders<Shop>.Filter.Eq("ID", ObjectId.Parse(id));
            var shop =  Shops.Find(filterS).First();

            var owner = Users.Find<User>(u => u.Shop.Id == shop.ID).First();
            var filterU = Builders<User>.Filter.Eq("ID", owner.ID);

            this.Service.DeleteImage(shop.Picture, IUserService.ImageType.Shop);
            foreach(Item item in shop.Items)
                this.Service.DeleteImage(item.Picture, IUserService.ImageType.Item);

            Shops.DeleteOne(filterS);
            Users.DeleteOne(filterU);
            return Ok();
        }

        [HttpPost]
        [Route("Shop/Search")]
        public async Task<IActionResult> SearchShops([FromBody] SearchRequest search)
        {
            var filter = Builders<Shop>.Filter.Empty;
            // Name search
            if(!string.IsNullOrEmpty(search.Name))
            {
                var regex = new Regex(search.Name, RegexOptions.IgnoreCase);
                var queryExpr = new BsonRegularExpression(regex); 
                var filterName = Builders<Shop>.Filter.Regex("Name", queryExpr);
                filter &= filterName;
            }

            // Tag search
            if(search.Tags != null && search.Tags.Count != 0)
            {
                var tagStr = String.Join(" ", search.Tags);
                var keys = Builders<Shop>.IndexKeys.Text("Tags");
                this.Shops.Indexes.CreateOne(keys);
                var filterTags = Builders<Shop>.Filter.Text(tagStr);
                filter &= filterTags;
            }

            return Ok(this.Shops.Find(filter).ToList());
        }

        [HttpGet]
        [Route("Shop/{ShopID}/Statistics")]
        public async Task<IActionResult> GetStatistics(string ShopID)
        {
            // TODO: Auth

            var OrdersByHour = this.OrderLists.AsQueryable()
                .Where(ol => ol.ID == ShopID)
                .SelectMany(ol => ol.History, (ol, o) => new
                {
                    Orders = o
                })
                .GroupBy(ol => ol.Orders.Hour)
                .Select(ol => new
                {
                    Hour = ol.Key,
                    Orders = ol.Select(o => o.Orders).Count()
                })
                .ToList();

            var ResponseTime = this.OrderLists.AsQueryable()
                .Where(o => o.ID == ShopID)
                .Select(o => o.History.Average(ord => ord.CompletitionTime - ord.OrderTime )
                )
                .FirstOrDefault();
            

            return Ok(new { OrdersByHour, ResponseTime });
        }

        #region shop item methods

        [HttpPost]
        [Route("Shop/{id}/Item")]
        public async Task<IActionResult> AddItem(string id, [FromForm] ItemRequest newItem)
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsOwner(username, id)) return BadRequest(new { message = "User does not own shop." });
   

            string img_path = this.Service.AddImage(newItem.Picture);

            List<string> tagList = new List<string>();
            if (newItem.Tags != null)
                tagList = newItem.Tags.Split(',').ToList<string>();

            Item item = new Item
            {
                Name = newItem.Name,
                Description = newItem.Description,
                Picture = img_path,
                Price = (double)newItem.Price,
                Amount = 1,
                Tags = tagList
            };

            Shop shop = this.Service.GetShop(id);
            if (shop.Items == null)
            {
                shop.Items = new List<Item>();
            }
            else if (shop.Items.Any(x => String.Equals(x.Name, item.Name)))
            {
                return BadRequest(new { message = "Prodavnica vec ima ovaj proizvod." });
            }

            shop.Items.Add(item);
            this.Service.UpdateShop(shop);
            return Ok(new { ItemID = item.ID});
        }

        [HttpPut]
        [Route("Shop/{shopid}/Item/{itemid}")] // TODO: fix method
        public async Task<IActionResult> UpdateItem(string shopid, string itemid, [FromForm] ItemRequest newItem) 
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsOwner(username, shopid)) return BadRequest(new { message = "User does not own shop." });
         

            Shop shop = this.Service.GetShop(shopid /*user.Shop.Id.AsString*/);
            if (shop.Items != null && shop.Items.Any(x => String.Equals(x.ID.ToString(), itemid)))
            {
                int index = shop.Items.FindIndex(x => String.Equals(x.ID.ToString(), itemid));
                if (newItem.Description != null)
                {
                    shop.Items[index].Description = newItem.Description;
                }
                if (newItem.Name != null)
                {
                    shop.Items[index].Name = newItem.Name;
                }
                if (newItem.Price != null)
                {
                    shop.Items[index].Price = (double)newItem.Price;
                }
                if (newItem.Picture != null)
                {
                    this.Service.DeleteImage(shop.Items[index].Picture, IUserService.ImageType.Item);
                    shop.Items[index].Picture = this.Service.AddImage(newItem.Picture, IUserService.ImageType.Item);
                }
                if (newItem.Tags != null)
                {
                    shop.Items[index].Tags = newItem.Tags.Split(',').ToList<string>();
                }
                this.Service.UpdateItem(shop.ID, shop.Items[index]);
                return Ok();
            }
            else
            {
                return BadRequest(new { message = "Shop does not have this item." });
            }
        }
    
        [HttpDelete]
        [Route("Shop/{shopid}/Item/{itemid}")]
        public async Task<IActionResult> DeleteItem(string shopid, string itemid) 
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsOwner(username, shopid)) return BadRequest(new { message = "User does not own shop." });
 

            Shop shop = this.Service.GetShop(shopid /*user.Shop.Id.AsString*/);
            if (shop.Items != null && shop.Items.Any(x => String.Equals(x.ID.ToString(), itemid)))
            {
                this.Service.DeleteItem(shop.ID, itemid);
                return Ok(); 
            }
            else
            {
                return BadRequest(new { message = "Shop does not have this item." });
            }
        }

        #endregion

        #endregion

        #region order methods

        [HttpGet]
        [Route("Shop/{id}/Order")]
        public async Task<IActionResult> GetShopOrders(string id) 
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsOwner(username, id)) return BadRequest(new { message = "User does not own shop." });
         

            var sfilter = Builders<OrderList>.Filter.Eq("ID", ObjectId.Parse(id));
            var orders = OrderLists.Find(sfilter).FirstOrDefault();
            if(orders == null)
                return Ok(new OrderList { 
                    ID=id,
                    Active = new List<Order>(),
                    History = new List<Order>()
                });
            else
                return Ok(orders);
        }

        [HttpPost]
        [Route("Shop/{id}/Order")]   
        public async Task<IActionResult> CreateOrder([FromBody] Order newOrder, string id) 
        {
            string shopID = id;
            var sfilter = Builders<Shop>.Filter.Eq("ID", ObjectId.Parse(shopID));
            var shop = Shops.Find(sfilter).First();
            
            var ofilter = Builders<OrderList>.Filter.Eq("ID", ObjectId.Parse(shopID));
            newOrder.ID = ObjectId.GenerateNewId().ToString();
            newOrder.OrderTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();

            bool OrderListsExists = Service.CollectionExists(dbSettings.Value.OrderCollectionName);

            var shopOrderList = OrderListsExists ? OrderLists.Find(ofilter).FirstOrDefault() : null ;

            newOrder.Hour = Int32.Parse(DateTime.Now.ToString("HH"));

            if (shopOrderList == null)
            {
                OrderList ol = new OrderList();
                ol.ID = shopID;
                ol.Active = new List<Order>();
                ol.History = new List<Order>();

                ol.Active.Add(newOrder);
                OrderLists.InsertOne(ol);
            }
            else
            {
                shopOrderList.Active.Add(newOrder);
                OrderLists.ReplaceOne(ofilter, shopOrderList);
            }
           
            return Ok(new {OrderID = newOrder.ID }); 
        }

        [HttpPut]
        [Route("Shop/{ShopID}/Order/{OrderID}/Complete")]
        public async Task<IActionResult> CompleteOrder(string ShopID, string OrderID)
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsOwner(username, ShopID)) return BadRequest(new { message = "User does not own shop." });
      

            bool OrderListsExists = Service.CollectionExists(dbSettings.Value.OrderCollectionName);
            if (!OrderListsExists) return BadRequest("Order not found");

            var ofilter = Builders<OrderList>.Filter.Eq("ID", ObjectId.Parse(ShopID));
            var orderlist = OrderLists.Find(ofilter).First();

            Order myOrder = orderlist.Active.FirstOrDefault(o => o.ID == OrderID);
            if (myOrder != null)
            {
                myOrder.CompletitionTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
                if (orderlist.History == null) 
                    orderlist.History = new List<Order>();
                orderlist.History.Add(myOrder);
                orderlist.Active.Remove(myOrder);
            }

            OrderLists.ReplaceOne(ofilter, orderlist);
            return Ok();
        }

        [HttpDelete]
        [Route("Shop/{ShopID}/Order/{OrderID}/Abort")]
        public async Task<IActionResult> AbortOrder(string ShopID, string OrderID)
        {
            string username = (string)HttpContext.Items["UserName"];
            if (!this.Service.IsOwner(username, ShopID)) return BadRequest(new { message = "User does not own shop." });
   

            bool OrderListsExists = Service.CollectionExists(dbSettings.Value.OrderCollectionName);
            if (!OrderListsExists) return BadRequest("Order not found");

            var ofilter = Builders<OrderList>.Filter.Eq("ID", ObjectId.Parse(ShopID));
            var orderlist = OrderLists.Find(ofilter).First();

            Order myOrder = orderlist.Active.First(o => o.ID == OrderID);
            if (myOrder != null)
            {
                orderlist.Active.Remove(myOrder);
            }

            OrderLists.ReplaceOne(ofilter, orderlist);
            return Ok();
        }

        [HttpGet]
        [Route("/Shop/PopularTags")]
        public async Task<IActionResult> GetPopular()
        {
            List<string> alltags = new List<string>();
            var tags = Shops.Aggregate()

                .Group(s => s.Tags,
                    z => new
                    {
                        Taglist = z.Key
                    }
                )
                .ToList()
                .SelectMany(t => t.Taglist)
                .GroupBy(vl => vl)
                .Select(x => new { Name = x.Key, Count = x.Count() })
                .OrderByDescending(z => z.Count)
                .Take(12);
                
            return Ok(tags);
        }

        [HttpGet]
        [Route("/Shop/Tags")]
        public async Task<IActionResult> GetAllShopTags()
        {
            List<string> alltags = new List<string>();
            var tags = Shops.Aggregate()

                .Group(s => s.Tags,
                    z => new
                    {
                        Taglist = z.Key
                    }
                )
                .ToList()
                .SelectMany(t => t.Taglist)
                .GroupBy(vl => vl)
                .Select(x => new { Name = x.Key }).Select(y => y.Name);
                
            return Ok(tags);
        }

        [HttpPost]
        [Route("/Shop/Near")]
        public async Task<IActionResult> GetNearShops([FromBody] LocationCoord coordinates)
        {
            var index = Builders<Shop>.IndexKeys.Geo2DSphere("Location");
            Shops.Indexes.CreateOne(index);

            var builder = Builders<Shop>.Filter;
            var point = new GeoJsonPoint<GeoJson2DCoordinates>(new GeoJson2DCoordinates(coordinates.Longitude, coordinates.Latitude));
            var filter = builder.Near(x => x.Location, point, maxDistance: 1500, minDistance: 1);

            var nearshops = Shops.Find(filter).ToList();
            return Ok(nearshops);
        }
        #endregion


    }
}

