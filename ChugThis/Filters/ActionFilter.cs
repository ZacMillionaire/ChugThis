using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nulah.ChugThis.Controllers.Users;
using Nulah.ChugThis.Models.Users;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Filters {
    public class ActionFilter : IActionFilter {

        private readonly IDatabase _redis;

        public ActionFilter(IDatabase Redis) {
            _redis = Redis;
        }
        public void OnActionExecuted(ActionExecutedContext context) {
        }

        /// <summary>
        /// Inject user data from data store before the action has started
        /// </summary>
        /// <param name="context"></param>
        public void OnActionExecuting(ActionExecutingContext context) {
            // Inject a PublicUser into ViewData
            var user = context.HttpContext.User;
            var ViewData = ( context.Controller as Controller ).ViewData;

            // create a blank user profile
            var UserData = new PublicUser();

            // If the user is authenticated (via OAuth), build a logged in PublicUser profile for the rest of the pipe line.
            // This runs every request so try to keep it very simple.
            // There's no reason you can't pull more useful data later on in a controller based on a property here.
            if(user.Identity.IsAuthenticated) {
                // create a PublicUser object with data from redis
                var UserKey = user.Claims.First(x => x.Type == "RedisKey").Value;
                UserData = UserController.GetUser(UserKey, _redis);

                // For now we'll just build a PublicUser profile from claims.
                // Usually I'd store something more complex in a database, then build a PublicUser in a Redis cache
                // and pull from there each request but for this project I've no real need to do so.
                // Plus, I don't really want to store real names in a database. In memory should be "safe" enough.
                // If there's some brand new brand name vuln that allows you to peak at memory for ASP I'll be wondering why the fuck
                // they aren't just going for API keys or something else.
                /*
                UserData.Id = user.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
                UserData.Name = user.Claims.First(x => x.Type == ClaimTypes.GivenName).Value;
                UserData.Provider = user.Claims.First().Subject.AuthenticationType;
                */
                UserData.isLoggedIn = true;
            }

            // Inject the PublicUser into view data for the rest of the request.
            ViewData.Add("User", UserData);
        }
    }
}
