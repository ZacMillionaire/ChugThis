using Nulah.ChugThis.Controllers.Charity;
using Nulah.ChugThis.Models;
using Nulah.ChugThis.Models.Maps;
using Nulah.ChugThis.Models.Users;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nulah.ChugThis.Models.Geo;
using Newtonsoft.Json;
using Nulah.ChugThis.Controllers.Users;

namespace Nulah.ChugThis.Controllers.Maps {
    public class MapController {

        private readonly IDatabase _redis;
        private readonly AppSettings _settings;
        private readonly string _baseMarkerKey;
        /// <summary>
        /// Geo Key, stores marker IDs to be HMGET from _entriesMarkersKey
        /// </summary>
        private readonly string _allMarkersKey;
        /// <summary>
        /// Hash set
        /// </summary>
        private readonly string _entriesMarkersKey;

        private const string MARKER_ID_COUNTER = "ID:Markers";

        public MapController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
            _baseMarkerKey = $"{_settings.ConnectionStrings.Redis.BaseKey}Markers";
            _allMarkersKey = $"{_baseMarkerKey}:All:GeoMarkers";
            _entriesMarkersKey = $"{_baseMarkerKey}:All:MarkerDetails";
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

        /// <summary>
        ///     <para>
        /// Returns the next marker ID
        ///     </para>
        ///     <para>
        /// As per Redis, if the key does not exist, it is created at 0, then incremented, then returns 1 as the next value
        ///     </para>
        /// </summary>
        /// <returns></returns>
        private long GetNextMarkerId() {
            var markerKey = $"{_settings.ConnectionStrings.Redis.BaseKey}{MARKER_ID_COUNTER}";
            return _redis.StringIncrement(markerKey);
        }

        public Feature NewGeoMarker(NewCharityMarker MarkerData, PublicUser User) {
            var charityController = new CharityController(_redis, _settings);
            var userController = new UserController(_redis, _settings);

            var charity = charityController.GetOrCreateCharityByName(MarkerData.Name);

            var charityMarker = new CharityMarker {
                Id = GetNextMarkerId(),
                CharityId = charity.Id,
                Doing = MarkerData.Doing,
                Location = MarkerData.Location,
                TimestampUTC = DateTime.UtcNow,
                UserId = User.GetUserId(),
                MarkerColour = charity.MarkerColour
            };

            AddGeoMarkerToAll(MarkerData.Location, charityMarker.Id);
            AddGeoMarkerToHashList(charityMarker);

            charityController.AddMarkerToCharity(charity.Name, charityMarker.Id);
            userController.AddMarkerToUser(User.GetUserId(), charityMarker.Id);

            return new Feature {
                Type = "Feature",
                Properties = new MarkerProperties {
                    MarkerColour = charity.MarkerColour,
                    MarkerId = charityMarker.Id
                },
                Geo = new Geometry {
                    LongLat = new double[] { charityMarker.Location.Longitude, charityMarker.Location.Latitude },
                    Type = "Point"
                }
            };
        }


        public FeatureCollection GetMarkersNearPoint(GeoLocation geoLocation, double Radius) {
            // Select all points in a radius around a location
            var Around = _redis.GeoRadius(_allMarkersKey, geoLocation.Longitude, geoLocation.Latitude, Radius, GeoUnit.Meters);
            // Get the marker Ids (later I'll probably want their distance values, but for now this is all I'm interested in.
            var MarkerIds = Around.Select(x => (double)x.Member)
                .ToArray();

            var markerDetails = _redis.HashGet(_entriesMarkersKey, Array.ConvertAll(MarkerIds, item => (RedisValue)item))
                .Select(x => JsonConvert.DeserializeObject<CharityMarker>(x).ToGeoJsonFeature());

            return new FeatureCollection {
                Type = "FeatureCollection",
                Features = markerDetails.ToArray()
            };
        }


        /// <summary>
        ///     <para>
        /// Adds a new geo marker, linked to a marker entry via MarkerId
        ///     </para>
        ///     <para>
        /// Creates an initial key if it has not been created previously.
        ///     </para>
        /// </summary>
        /// <param name="Location"></param>
        /// <param name="MarkerId"></param>
        /// <returns></returns>
        private void AddGeoMarkerToAll(GeoLocation Location, long MarkerId) {

            _redis.GeoAdd(_allMarkersKey, new GeoEntry(Location.Longitude, Location.Latitude, MarkerId));

            try {
                // verify it was created
                var added = _redis.GeoPosition(_allMarkersKey, MarkerId).Value;
            } catch(Exception e) {
                throw new NullReferenceException($"Redis exception on getting GeoPostion of MarkerId: {MarkerId} from {_allMarkersKey} (How?)", e);
            }

        }

        /// <summary>
        ///     <para>
        /// Stores the details of an added marker, to be referenced from the geo list    
        ///     </para>
        ///     <para>
        /// Creates an initial key if it has not been created previously.
        ///     </para>
        /// </summary>
        /// <param name="NewMarker"></param>
        /// <returns></returns>
        private void AddGeoMarkerToHashList(CharityMarker NewMarker) {
            _redis.HashSet(_entriesMarkersKey, NewMarker.Id, NewMarker.ToString(), When.Always, CommandFlags.FireAndForget);
        }
    }

    // TODO: move this shit to seperate classes
    public class CharityMarker {
        /// <summary>
        /// Marker Id
        /// </summary>
        public long Id { get; set; }
        public long CharityId { get; set; }
        /// <summary>
        /// Redis identifier: ProviderShort-ProviderId
        /// </summary>
        public string UserId { get; set; }
        public GeoLocation Location { get; set; }
        // I'll be using this to filter within a sliding timespan of -1 hour of the users time
        // The users time is also in UTC, for what its worth
        public DateTime TimestampUTC { get; set; }
        public string[] Doing { get; set; }
        // this'll cause a bunch of fun later when I need to go over all the markers
        // and find the ones for a charity to update their marker colours.
        public string MarkerColour { get; set; }

        /// <summary>
        ///     <para>
        /// Overridden.
        ///     </para>
        ///     <para>
        /// Returns the JSON representation of the object.
        ///     </para>
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }

        public Feature ToGeoJsonFeature() {
            return new Feature {
                Type = "Feature",
                Properties = new MarkerProperties {
                    MarkerColour = MarkerColour,
                    MarkerId = Id
                },
                Geo = new Geometry {
                    LongLat = new double[] { Location.Longitude, Location.Latitude },
                    Type = "Point"
                }
            };
        }
    }

    /// <summary>
    /// To be returned for displaying on a map.
    /// </summary>
    // I probably don't need all of these JsonProperties, but who knows what
    // fun new ways JavaScript will fail over basic things.
    // I should probably just change this to be a generic feature class for GeoJson as well...
    // ...
    // Eh, later
    public class Feature {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("properties")]
        public MarkerProperties Properties { get; set; }
        [JsonProperty("geometry")]
        public Geometry Geo { get; set; }
    }

    public class MarkerProperties {
        /// <summary>
        /// From the colour of the charity
        /// </summary>
        [JsonProperty("Marker-Colour")]
        public string MarkerColour { get; set; }
        [JsonProperty("Marker-Id")]
        public long MarkerId { get; set; }
        // In future I'll probably want to expand this a bit for desktop users
        // so they can see a list of all markers with details in another div.
        // For now I'll just leave it as these 2 for marker interaction and
        // loading additional data after the fact.
        // The less data loaded over mobile data the better.
    }

    public class Geometry {
        [JsonProperty("type")]
        public string Type { get; set; }

        // Should probably put some long/lat validation here.
        // TODO: change this to a GeoLocation object, then add a ToString
        // override to turn it into the array we need
        /// <summary>
        /// Coordinates
        /// </summary>
        [JsonProperty("coordinates")]
        public double[] LongLat { get; set; }
    }
    public class FeatureCollection {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("features")]
        public Feature[] Features { get; set; }
    }
}
