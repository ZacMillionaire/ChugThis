using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Nulah.ChugThis.Models;
using Nulah.ChugThis.Controllers.Maps;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Nulah.ChugThis.Areas.Index.Controllers {
    [Area("Index")]
    public class HomeController : Controller {

        private readonly IDatabase _redis;
        private readonly AppSettings _settings;

        public HomeController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
        }

        [Route("~/")]
        [HttpGet]
        public IActionResult Index() {
            // Pull the current key from the cache.
            // This is done every request instead of startup, in case a key needs to be rotated because some cheeky
            // fuck decided to lift our key for their own use.
            ViewData["MapBoxKey"] = MapController.GetMapBoxApiKey(_redis, _settings);
            return View();
        }
    }
}
