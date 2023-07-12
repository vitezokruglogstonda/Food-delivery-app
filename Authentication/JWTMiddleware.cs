using MasterFood.Service;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MasterFood.Authentication
{
    public class JWTMiddleware
    {
        private readonly RequestDelegate _next;

        public JWTMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, IUserService service)
        {
            string token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (token != null)
                await AttachUser(context, service, token);

            await _next(context);
        }

   
        private async Task AttachUser(HttpContext context, IUserService service, string token)
        {
            try
            {
                (string username, IUserService.AccountType type)? user = service.CheckToken(token);
                if (user != null)
                {
                    context.Items["UserName"] = user.Value.username;
                    context.Items["UserType"] = user.Value.type;
                }
            }
            catch
            { }
        }
    }
}
