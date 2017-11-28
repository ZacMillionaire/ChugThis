using Newtonsoft.Json;
using Nulah.ChugThis.Models.Maps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models.Users {
    public class PublicUser {
        public long Id { get; set; }
        public string Provider { get; set; }
        public string ProviderShort { get; set; }
        public string Name { get; set; }
#pragma warning disable IDE1006 // Naming Styles
        [JsonIgnore]
        public bool isLoggedIn { get; set; }
        public bool isExpressMode { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        public DateTime LastSeenUTC { get; set; }
        public ZoomOptions Zoom { get; set; }
        public MarkerOptions Marker { get; set; }
        public Preferences Preferences { get; set; }


        public PublicUser() {
            isLoggedIn = false;
            Zoom = new ZoomOptions();
            Marker = new MarkerOptions();
            Preferences = new Preferences();
        }

        /// <summary>
        ///     <para>
        /// Returns the users Redis Id: {ProviderShort}-{Id}
        ///     </para>
        /// </summary>
        /// <returns></returns>
        public string GetUserId() {
            return $"{ProviderShort}-{Id}";
        }
    }
}
