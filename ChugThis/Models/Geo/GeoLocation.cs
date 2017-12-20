using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models.Geo {
    // .Net core doesn't have GeoCoordinate because...who knows
    // So here's a lazy version
    public class GeoLocation {
        private double _longitude { get; set; }
        private double _latitude { get; set; }

        public double Latitude
        {
            get { return _latitude; }
            set
            {
                if(value > 90.0 || value < -90.0) {
                    throw new ArgumentOutOfRangeException("Latitude", "Argument must be in range of -90 to 90");
                }
                _latitude = value;
            }
        }

        public double Longitude
        {
            get { return _longitude; }
            set
            {
                if(value > 180.0 || value < -180.0) {
                    throw new ArgumentOutOfRangeException("Longitude", "Argument must be in range of -180 to 180");
                }
                _longitude = value;
            }
        }

        public GeoLocation(double Longitude, double Latitude) {
            this.Longitude = Longitude;
            this.Latitude = Latitude;
        }
    }
}
