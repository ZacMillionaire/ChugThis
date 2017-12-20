﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nulah.ChugThis.Models.Maps;
using StackExchange.Redis;
using Nulah.ChugThis.Models;
using Nulah.ChugThis.Models.Users;
using Nulah.ChugThis.Models.Geo;
using Nulah.ChugThis.Filters;

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
        [UserFilter(RequiredLoggedInState: true)]
        [ValidateAntiForgeryToken]
        public IActionResult AddNewCharityMarkerNoScript([FromForm]NewCharityMarker FormData) {
            // The user somehow gets the form to display, and fill out a location without...javascript...
            var user = (PublicUser)ViewData["User"];
            var map = new MapController(_redis, _settings);
            var addedMarker = map.NewGeoMarker(FormData, user);
            return RedirectToAction("Index", "Home", new { area = "Index" });
        }

        [HttpPost]
        [Route("~/Api/Add/NewMarker")]
        [UserFilter(RequiredLoggedInState: true)]
        [ValidateAntiForgeryToken]
        public Feature AddNewCharityMarker([FromForm]NewCharityMarker FormData) {
            var user = (PublicUser)ViewData["User"];
            var map = new MapController(_redis, _settings);
            var addedMarker = map.NewGeoMarker(FormData, user);
            return addedMarker;
        }

        /// <summary>
        ///     <para>
        /// Returns a list of markers around a point.
        ///     </para>
        /// </summary>
        /// <param name="Longitude"></param>
        /// <param name="Latitude"></param>
        /// <param name="Radius"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("~/Api/GetMarkers")]
        public FeatureCollection GetMarkersNearPoint(double Longitude, double Latitude, double Radius) {
            var map = new MapController(_redis, _settings);
            var markerFeatureCollection = map.GetMarkersNearPoint(new GeoLocation(Longitude, Latitude), Radius);
            return markerFeatureCollection;
        }

        /// <summary>
        ///     <para>
        /// Returns a list of markers around a point with full details
        ///     </para>
        /// </summary>
        /// <param name="Longitude"></param>
        /// <param name="Latitude"></param>
        /// <param name="Radius"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("~/Api/GetMarkerDetails")]
        public List<CharityMarker> GetMarkerDetails(double Longitude, double Latitude, double Radius) {
            var map = new MapController(_redis, _settings);
            var markerFeatureCollection = map.GetCharityDetailsNearPoint(new GeoLocation(Longitude, Latitude), Radius);
            return markerFeatureCollection;
        }
    }
}
