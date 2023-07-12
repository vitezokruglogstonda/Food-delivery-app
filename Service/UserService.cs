using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MasterFood.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Collections.Generic;
using MongoDB.Bson;
using System.Security.Cryptography;

namespace MasterFood.Service
{
    public interface IUserService
    {
        enum OrderStatus
        {
            Waiting,
            OnTheWay,
            Done
        };
        enum ImageType
        {
            Shop,
            Item
        };
        enum AccountType
        {
            Shop,
            Admin
        };

        string AddImage(IFormFile? image, ImageType img_type = IUserService.ImageType.Item);
        bool DeleteImage(string image, IUserService.ImageType img_type);
        string GenerateToken(string id);
        (string username, IUserService.AccountType type)? CheckToken(string token);
        bool CheckPassword(byte[] password, byte[] salt, string password_string);
        void CreatePassword(out byte[] password, out byte[] salt, string password_string);
        User FindUser(string id);
        List<Shop> GetAllShops(int page_num, int page_size);
        Shop GetShop(string id);
        void UpdateShop(Shop shop);
        void UpdateItem(string id, Item item);
        void DeleteItem(string id, string itemid);
        User? GetUser(string? id, string username = "");
        void StoreShop(Shop shop, User user);
        void CreateUser(User new_user = null);
        void LogUser(string id, bool status);

        public bool CollectionExists(string name);
        public bool IsOwner(string username, string shopID);
        public bool IsAdmin(string username);
    }

    public class UserService : IUserService
    {
        public IWebHostEnvironment Environment { get; set; }
        public AppSettings _appSettings { get; set; }

        private readonly IMongoCollection<Shop> Shops;
        private readonly IMongoCollection<Order> Orders;
        private readonly IMongoCollection<User> Users;
        private readonly IMongoDatabase database;

        public UserService(IWebHostEnvironment environment, IOptions<DbSettings> dbSettings, IOptions<AppSettings> appsettings) {
            this.Environment = environment;
            this._appSettings = appsettings.Value;
            MongoClient client = new MongoClient(dbSettings.Value.ConnectionString);
            database = client.GetDatabase(dbSettings.Value.DatabaseName);
            this.Shops = database.GetCollection<Shop>(dbSettings.Value.ShopCollectionName);
            this.Orders = database.GetCollection<Order>(dbSettings.Value.OrderCollectionName);
            this.Users = database.GetCollection<User>(dbSettings.Value.UserCollectionName);
        }

        public string AddImage(IFormFile? image, IUserService.ImageType img_type = IUserService.ImageType.Item)
        {
            string folderPath = "Images/"+ img_type.ToString();
            string uploadsFolder = Path.Combine(Environment.WebRootPath, folderPath);
            string file_name;
            if (image != null)
            {
                file_name = Guid.NewGuid().ToString() + "_" + image.FileName;
                string filePath = Path.Combine(uploadsFolder, file_name);
                image.CopyTo(new FileStream(filePath, FileMode.Create));
            }
            else
            {
                file_name = "default.png";
            }
            return file_name;
        }
        
        public bool DeleteImage(string image, IUserService.ImageType img_type)
        {
            if(!String.Equals(image, "default.png"))
            {
                string folderPath = "Images/" + img_type.ToString();
                string uploadsFolder = Path.Combine(Environment.WebRootPath, folderPath);
                string filePath = Path.Combine(uploadsFolder, image);

                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            return true;
        }

        public string GenerateToken(string id)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            byte[] key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("id", id) }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public (string username, IUserService.AccountType type)? CheckToken(string token)
        {
            if (!String.IsNullOrEmpty(token))
            {
                (string username, IUserService.AccountType type) result;

                JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
                byte[] key = Encoding.ASCII.GetBytes(_appSettings.Secret);
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.FromMinutes(10)
                }, out SecurityToken validatedToken);

                JwtSecurityToken jwtToken = (JwtSecurityToken)validatedToken;
                //int userID = int.Parse(jwtToken.Claims.First(x => x.Type == "id").Value);
                string userID = jwtToken.Claims.First(x => x.Type == "id").Value;

                User user = this.FindUser(userID);
                result.username = user.UserName;
                result.type = user.UserType;

                return result;
            }
            else
            {
                return null;
            }
        }
        
        public bool CheckPassword(byte[] password, byte[] salt, string password_string)
        {
            HMACSHA512 hashObj = new HMACSHA512(salt);
            byte[] byte_password = System.Text.Encoding.UTF8.GetBytes(password_string);
            byte[] hash = hashObj.ComputeHash(byte_password);
            int len = hash.Length;
            for (int i = 0; i < len; i++)
            {
                if (password[i] != hash[i])
                {
                    return false;
                }
            }
            return true;
        }

        //CreatePassword(out byte[] password, out byte[] salt, string password_string)
        public void CreatePassword(out byte[] password, out byte[] salt, string password_string)
        {
            HMACSHA512 hashObj = new HMACSHA512();
            salt = hashObj.Key;
            byte[] byte_password = Encoding.UTF8.GetBytes(password_string);
            password = hashObj.ComputeHash(byte_password);
        }

        public User FindUser(string id)
        {
            return this.Users.Find(u => u.ID == id).FirstOrDefaultAsync().Result;
        }

        public List<Shop> GetAllShops(int page_num, int page_size)
        {
            FilterDefinition<Shop> filter = Builders<Shop>.Filter.Empty;
            return this.Shops.Find(filter).Skip((page_num-1)*page_size).Limit(page_size).ToList();
        }

     

        public Shop GetShop(string id)
        {
            //FilterDefinition<Shop> filter = Builders<Shop>.Filter.Eq("_id", id);
            FilterDefinition<Shop> filter = Builders<Shop>.Filter.Eq(s => s.ID, id);
            return this.Shops.Find(filter).FirstOrDefault();
        }

        public User? GetUser(string? id, string username = "")
        {
            FilterDefinition<User> filter;
            if (id != null)
            {
                //filter = Builders<User>.Filter.Eq("_id", id);
                filter = Builders<User>.Filter.Eq(u=> u.ID, id);
            }
            else if(!String.IsNullOrEmpty(username))
            {
                filter = Builders<User>.Filter.Eq(p => p.UserName, username);
            }
            else
            {
                return null;
            }
            return this.Users.Find(filter).FirstOrDefault();
        }       

        public void UpdateShop(Shop shop)
        {
            //FilterDefinition<Shop> filter = Builders<Shop>.Filter.Eq("_id", shop.ID);
            FilterDefinition<Shop> filter = Builders<Shop>.Filter.Eq(s => s.ID, shop.ID);
            this.Shops.ReplaceOne(filter, shop);
        }

        public void UpdateItem(string id, Item item)
        {
            //ili umesto 1. Filter.And(), ugnezdene u njemu mozes da vezes sa & (ne &&)
            FilterDefinition<Shop> filter = Builders<Shop>.Filter.And(
                    Builders<Shop>.Filter.Eq(s => s.ID, id),
                    Builders<Shop>.Filter.ElemMatch(x => x.Items, Builders<Item>.Filter.Eq(i => i.ID, item.ID))
                );
            //filter selektuje svaki Shop koji se poklapa sa id-jem, i koji u svojoj listi x.Items sadrzi bilo koji element koji:
                //(desna strana posle zareza u ElemMatch)
                //je Item dokument, i ima Name == item.Name

            var update = Builders<Shop>.Update.Set(x => x.Items[-1], item);
            //bilo koji element koji bude "selektovan" filterom, ovaj update ce se izvrsiti nad njim
                //bilo koji selektovani element koji zadovoljava filter, u listi x.Items, bice zamenjen sa item
                //(list[-1] znaci isto sto i list.$, a to znaci da se selektuje element cija je pozicija varijabilna, tj moze biti na bilo kojoj poziciji)

            this.Shops.UpdateOne(filter, update);
        }

        public void DeleteItem(string shopid, string itemid) {
            var filterShop = Builders<Shop>.Filter.Eq(s => s.ID, shopid);
            var filterItem = Builders<Item>.Filter.Eq(s => s.ID, itemid);
            var update = Builders<Shop>.Update.PullFilter(s => s.Items, filterItem);
            this.Shops.UpdateOne(filterShop, update);
        }

        public void StoreShop(Shop shop, User user)
        {
            //FilterDefinition<User> userFilter = Builders<User>.Filter.Eq("_id", user.ID);
          //g
          //  this.Shops.InsertOne(shop);
          //  //user.Shop = new MongoDBRef("Shop", shop.ID);
          //  user.Shop = new MongoDBRef("Shop", BsonValue.Create(shop.ID));
          //  this.Users.ReplaceOne(userFilter, user);
        }

        public void CreateUser(User new_user = null)
        {
            if (new_user == null)
            {
                User user = this.GetUser(null, IUserService.AccountType.Admin.ToString());
                if (user == null)
                {
                    byte[] password, salt;
                    this.CreatePassword(out password, out salt, this._appSettings.AdminPassword);
                    user = new User
                    {
                        UserType = IUserService.AccountType.Admin,
                        UserName = this._appSettings.AdminUserName,
                        Password = password,
                        Salt = salt,
                        Online = false,
                        Shop = null
                    };
                    this.Users.InsertOne(user);
                }
            }
            else
            {
                this.Users.InsertOne(new_user);
            }
        }

        public void LogUser(string id, bool status)
        {
            FilterDefinition<User> userFilter = Builders<User>.Filter.Eq(u => u.ID, id);
            //UpdateDefinition<User> userUpdate = Builders<User>.Update.Set("online", status);
            UpdateDefinition<User> userUpdate = Builders<User>.Update.Set(x => x.Online, status);
            this.Users.UpdateOne(userFilter, userUpdate);
        }

        //public T LoadRecordByID<T>(IMongoCollection<T> collection, int id)
        //{
        //    var filter = Builders<T>.Filter.Eq("ID", id);
        //    return collection.Find(filter).First();
        //}

        public void DeleteRecordByID<T>(IMongoCollection<T> collection, int id)
        {
            var filter = Builders<T>.Filter.Eq("Id", id);
            collection.DeleteOne(filter);
        }

        public bool CollectionExists(string name)
        {

            var filterr = new BsonDocument("name", name);
            var options = new ListCollectionNamesOptions { Filter = filterr };
            return database.ListCollectionNames(options).Any();
        }

        public bool IsOwner(string username, string shopID)
        {
            if (_appSettings.Auth == false) return true; //if auth is disabled
            
            User user = this.GetUser(null, username);
            if (user == null) return false;
            if (user.UserType == IUserService.AccountType.Admin) return true;

            Shop shop = this.GetShop(user.Shop.Id.AsString);
            return shop.ID == shopID;
        }
        
        public bool IsAdmin(string username)
        {
            if (_appSettings.Auth == false) return true; //if auth is disabled

            User user = this.GetUser(null, username);
            if (user == null) return false;    
            return (user.UserType == IUserService.AccountType.Admin);
        }

      
    }
}
