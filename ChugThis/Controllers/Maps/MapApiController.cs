using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nulah.ChugThis.Models.Maps;
using StackExchange.Redis;
using Nulah.ChugThis.Models;
using Nulah.ChugThis.Models.Users;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Nulah.ChugThis.Controllers.Maps {

    public class MapApiController : Controller {

        private readonly IDatabase _redis;
        private readonly AppSettings _settings;

        public MapApiController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
        }


        [HttpPost]
        [Route("~/Add/NewMarker")]
        [ValidateAntiForgeryToken]
        public void AddNewCharityMarker([FromForm]NewCharityMarker FormData) {
            var user = (PublicUser)ViewData["User"];
            var map = new MapController(_redis, _settings);
            map.AddGeoMarkerToAll(FormData, user);
        }
    }
}
