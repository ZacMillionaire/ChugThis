using Nulah.ChugThis.Models;
using Nulah.ChugThis.Models.Maps;
using Nulah.ChugThis.Models.Users;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Controllers.Maps {
    public class MapController {

        private readonly IDatabase _redis;
        private readonly AppSettings _settings;
        private readonly string _allMarkersKey;
        private readonly string _baseMarkerKey = "Markers";

        public MapController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
            _baseMarkerKey = $"{_settings.ConnectionStrings.Redis.BaseKey}Markers";
            _allMarkersKey = $"{_baseMarkerKey}:All";
        }

        /// <summary>
        ///     <para>
        /// Used to set or update the stored MapBox Api Key.
        ///     </para><para>
        ///     
        ///     </para>
        /// </summary>
        /// <param name="Redis"></param>
        /// <param name="Settings"></param>
        internal static void SetMapBoxApiKey(IDatabase Redis, AppSettings Settings) {
            var ApiHashKey = $"{Settings.ConnectionStrings.Redis.BaseKey}Keys";

            if(!Redis.HashExists(ApiHashKey, "MapBox")) {
                Redis.HashSet(ApiHashKey, "MapBox", Settings.ApiKeys.MapBox);
            } else {
                if(Redis.HashGet(ApiHashKey, "MapBox") != Settings.ApiKeys.MapBox) {
                    Redis.HashSet(ApiHashKey, "MapBox", Settings.ApiKeys.MapBox);
                }
            }
        }

        /// <summary>
        ///     <para>
        /// Returns the current stored MapBox Api Key
        ///     </para>
        /// </summary>
        /// <returns></returns>
        internal static string GetMapBoxApiKey(IDatabase Redis, AppSettings Settings) {
            var ApiHashKey = $"{Settings.ConnectionStrings.Redis.BaseKey}Keys";
            var MapBoxKey = Redis.HashGet(ApiHashKey, "MapBox");
            return MapBoxKey;
        }

        public void AddGeoMarkerToAll(NewCharityMarker MarkerData, PublicUser User) {
            throw new NotImplementedException();
            //_redis.GeoAdd(_allMarkersKey)
        }

        public void AddGeoMarkerToSpecific() {
            throw new NotImplementedException();
        }

        public void AddMarkerToUser(PublicUser User) {
            throw new NotImplementedException();
        }

    }
}
