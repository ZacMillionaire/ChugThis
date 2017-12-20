using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models.Maps {
    public class MarkerOptions {
        /// <summary>
        /// Size of the add feature marker when a user click/taps an empty space on the map
        /// </summary>
        public int AddMarkerSize { get; set; }
        /// <summary>
        /// Base size in pixels that markers will default to
        /// </summary>
        public int FeatureMarkerBaseSize { get; set; }

        public MarkerOptions() {
            AddMarkerSize = 25;
            FeatureMarkerBaseSize = 20;
        }
    }
}
