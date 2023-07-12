using MasterFood.Models;
using MasterFood.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterFood.Authentication
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthAttribute : Attribute, IAuthorizationFilter
    {
        public bool JustAdmin { get; set; }
        public AuthAttribute()
        {
            this.JustAdmin = false;
        }
        public AuthAttribute(string type)
        {
            if (String.Equals(type, IUserService.AccountType.Admin.ToString()))
            {
                this.JustAdmin = true;
            }
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            string username = (string)context.HttpContext.Items["UserName"];
            if (username == null)
            {
                context.Result = new JsonResult(new { message = "Neovlascen" }) { StatusCode = StatusCodes.Status401Unauthorized };
            }
            else if (this.JustAdmin && IUserService.AccountType.Admin != (IUserService.AccountType)context.HttpContext.Items["UserType"])
            {
                context.Result = new JsonResult(new { message = "Neovlascen" }) { StatusCode = StatusCodes.Status401Unauthorized };
            }
        }
    }
}
