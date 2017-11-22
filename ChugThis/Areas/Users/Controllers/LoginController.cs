using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nulah.ChugThis.Filters;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Nulah.ChugThis.Models.Users;
using StackExchange.Redis;
using Nulah.ChugThis.Models;
using Nulah.ChugThis.Controllers.Users;

namespace ChugThis.Areas.Users.Controllers {
    [Area("Users")]
    public class LoginController : Controller {

        private readonly IDatabase _redis;
        private readonly AppSettings _settings;

        public LoginController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
        }

        [Route("~/Login")]
        [UserFilter(RequiredLoggedInState: false)]
        public IActionResult Login() {
            ViewData["Providers"] = _settings.OAuthProviders;
            return View();
        }

        // TODO: Sort out the routing and such for when a user denies access on the provider end.
        [Route("~/Login/{Provider}")]
        [UserFilter(RequiredLoggedInState: false)]
        public async Task<IActionResult> LoginWithProvider(string error, string error_description, string Provider) {
            if(error == null && error_description == null) {
                var challenge = HttpContext.ChallengeAsync(Provider, properties: new AuthenticationProperties {
                    RedirectUri = "/"
                });
                await challenge;
            }

            return View();
        }

        [Route("~/Logout")]
        [UserFilter(RequiredLoggedInState: true, Redirect: "~/Login")]
        public async Task<IActionResult> Logout() {
            var signOut = HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await signOut;

            if(signOut.IsCompleted) {

                var userRedisKey = HttpContext.User.Claims.First(x => x.Type == "RedisKey").Value;
                var userController = new UserController(_redis, _settings);

                userController.LogUserOut(( (PublicUser)ViewData["User"] ).Id);

                // Blank the PublicUser profile so the view doesn't have lingering logged in side effects
                ViewData["User"] = new PublicUser();
            }

            return View();
        }
    }
}