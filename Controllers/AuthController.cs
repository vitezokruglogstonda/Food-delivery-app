using MasterFood.Authentication;
using MasterFood.Models;
using MasterFood.RequestResponse;
using MasterFood.Service;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : Controller
    {
        public IUserService Service { get; set; }

        public AuthController(IUserService service)
        {
            this.Service = service;
        }

        [HttpPost]
        [Route("CreateAdmin")]
        public async Task<IActionResult> CreateAdmin()
        {
            this.Service.CreateUser();
            return Ok();
        }

        //[Auth("Admin")]
        [HttpPost]
        [Route("CreateOwner")]
        public async Task<IActionResult> CreateOwner([FromBody] NewOwner request)
        {
            User user = this.Service.GetUser(null, request.UserName);
            if (user != null)
            {
                return BadRequest(new { message = "User already exists." });
            }
            byte[] password, salt;
            this.Service.CreatePassword(out password, out salt, request.Password);
            user = new User
            {
                UserName = request.UserName,
                Password = password,
                Salt = salt,
                Online = false,
                Shop = null
            };
            this.Service.CreateUser(user);
            return Ok();
        }

        [HttpPut]
        [Route("LogIn")]
        public async Task<IActionResult> LogIn([FromBody] LogInRequest request)
        {
            User user = this.Service.GetUser(null, request.UserName);
            if (user == null)
            {
                return BadRequest(new { message = "User does not exist." });
            }
            if (!this.Service.CheckPassword(user.Password, user.Salt, request.Password))
            {
                return BadRequest(new { message = "Wrong password." });
            }
            this.Service.LogUser(user.ID, true);
            LogInResponse response = new LogInResponse
            {
                ID = user.ID,
                UserName = user.UserName,
                Token = this.Service.GenerateToken(user.ID),
                Level = user.UserType,
                ShopID = user.Shop == null ? null : user.Shop.Id.ToString()
            };
            return Ok(response);
        }

        [Auth]
        [HttpPut]
        [Route("LogOut")]
        public async Task<IActionResult> LogOut()
        {
            User user = this.Service.GetUser(null, (string)HttpContext.Items["UserName"]);
            this.Service.LogUser(user.ID, false);
            return Ok();
        }

    }
}
