using Nulah.ChugThis.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Controllers.Maps {
    public class MapController {

        private readonly IDatabase _redis;
        private readonly AppSettings _settings;

        public MapController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
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

    }
}
