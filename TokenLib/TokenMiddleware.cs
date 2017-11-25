using Entity.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace TokenLib
{
    public class TokenMiddleware
    {



        private readonly RequestDelegate _next;
        private readonly TokenOptions _options;
        private StudentContext _accountcontext;

        public TokenMiddleware(
            RequestDelegate next,
            StudentContext accountcontext,
            IOptions<TokenOptions> options)
        {
            _next = next;
            _options = options.Value;
            _accountcontext = accountcontext;
        }

        public Task Invoke(HttpContext context)
        {
            // If the request path doesn't match, skip
            if (!context.Request.Path.Equals(_options.Path, StringComparison.Ordinal))
            {
                return _next(context);
            }

            // Request must be POST with Content-Type: application/x-www-form-urlencoded
            if (!context.Request.Method.Equals("POST")
                || !context.Request.HasFormContentType)
            {
                context.Response.StatusCode = 400;
                return context.Response.WriteAsync("Bad request.");
            }

            return GenerateToken(context);
        }

        private int CheckIdentity(string username, string password)
        {
            int userId = 0;

            var user = _accountcontext.Users.FirstOrDefault(item => item.UserName == username && item.Password == password);

            if (user != null)
            {
                userId = user.ID;
            }

            return userId;
        }

        private async Task GenerateToken(HttpContext context)
        {

            var grant_type = context.Request.Form["grant_type"];
            int userId = 0;

            if (grant_type == "password")
            {
                var username = context.Request.Form["username"];
                var password = context.Request.Form["password"];
                userId = CheckIdentity(username, password);
            }
            else
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid grant type.");
                return;

            }



            if (userId == 0)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid username or password.");
                return;
            }



            var now = DateTime.UtcNow;
            var nowOffset = new DateTimeOffset(now);

            var claimUserID = new Claim("userID", userId.ToString());
            var claimName = new Claim(ClaimTypes.Name, "Primary");
            var claimRole = new Claim(ClaimTypes.Role, "Officer");
            var claims = new Claim[]
            {
                claimUserID,
                claimName,
                claimRole
            };



            // Create the JWT and write it to a string
            var jwt = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: now.Add(_options.Expiration),
                signingCredentials: _options.SigningCredentials
            );


            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            var response = new
            {
                access_token = encodedJwt,
                expires_in = (int)_options.Expiration.TotalSeconds


            };

            // Serialize and return the response
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(response, new JsonSerializerSettings { Formatting = Formatting.Indented }));

        }


    }
}
