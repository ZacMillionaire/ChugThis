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
                UserId = User.GetUserId()
            };

            AddGeoMarkerToAll(MarkerData.Location, charityMarker.Id);
            AddGeoMarkerToHashList(charityMarker);

            charityController.AddMarkerToCharity(charity.Name, charityMarker.Id);
            userController.AddMarkerToUser(User.GetUserId(), charityMarker.Id);

            return new Feature {
                Type = "Feature",
                Properties = new MarkerProperties {
                    MarkerPrimary = charity.Style.PrimaryColour,
                    MarkerSecondary = charity.Style.SecondaryColour,
                    MarkerIcon = charity.Style.Icon,
                    MarkerId = charityMarker.Id,
                    Opacity = 1.0,
                    Size = 1.0
                },
                Geo = new Geometry {
                    LongLat = new double[] { charityMarker.Location.Longitude, charityMarker.Location.Latitude },
                    Type = "Point"
                }
            };
        }

        /// <summary>
        ///     <para>
        /// Returns a class ready for GeoJson, returning markers a radius in meters around a given GeoLocation.
        ///     </para>
        ///     <para>
        /// Returned markers will be sized based on steps from current hour, back to 3 hours ago, from 1.0, 0.75, 0.5; opacity will be 1 for all.
        ///     </para>
        /// </summary>
        /// <param name="geoLocation"></param>
        /// <param name="Radius"></param>
        /// <returns></returns>
        public FeatureCollection GetMarkersNearPoint(GeoLocation geoLocation, double Radius) {
            return GetMarkers(geoLocation, Radius, false);
        }

        public List<CharityMarker> GetCharityDetailsNearPoint(GeoLocation geoLocation, double Radius) {
            var markerData = GetCharityMarkers(geoLocation, Radius);
            var charities = GetCharities(markerData.Select(x => x.CharityId).Distinct().ToArray()).Result;
            markerData.ForEach(x => x.CharityDetails = charities.First(y => y.Id == x.CharityId));
            return markerData;
        }

        /// <summary>
        ///     <para>
        /// Returns a class ready for GeoJson, returning markers a radius in meters around a given GeoLocation.
        ///     </para>
        ///     <para>
        /// Returned markers will be sized based on steps from current hour, back to 3 hours ago, from 1.0, 0.75, 0.5; opacity will be 1 for all.
        ///     </para>
        ///     <para>
        /// Total number of markers returned will be controlled by MarkerCap
        ///     </para>
        /// </summary>
        /// <param name="geoLocation"></param>
        /// <param name="Radius"></param>
        /// <param name="MarkerCap"></param>
        /// <returns></returns>
        public FeatureCollection GetMarkersNearPoint(GeoLocation geoLocation, double Radius, int MarkerCap) {
            return GetMarkers(geoLocation, Radius, false, MarkerCap);
        }

        /// <summary>
        ///     <para>
        /// Returns a class ready for GeoJson, returning markers a radius in meters around a given GeoLocation.
        ///     </para>
        ///     <para>
        /// Returned markers will be sized based on steps from current hour, back to 3 hours ago, from 1.0, 0.75, 0.5; opacity will be 1 for all.
        ///     </para>
        /// </summary>
        /// <param name="geoLocation"></param>
        /// <param name="Radius"></param>
        /// <param name="ShowHistoric"></param>
        /// <returns></returns>
        public FeatureCollection GetMarkersNearPoint(GeoLocation geoLocation, double Radius, bool ShowHistoric) {
            return GetMarkers(geoLocation, Radius, ShowHistoric);
        }

        /// <summary>
        ///     <para>
        /// Returns a class ready for GeoJson, returning markers a radius in meters around a given GeoLocation.
        ///     </para>
        ///     <para>
        /// If ShowHistoric is true, then return all markers ever made, at a size of 0.5 (50% of their base size), with an opacity of 1. If left as default,
        /// markers will be filtered based on hour steps from up to 3 hours old. Markers older than 3 hours will be half size and half opacity
        ///     </para>
        ///     <para>
        /// The number of markers is controlled by MarkerCap, and markers are ordered by distance from target before capping.
        ///     </para>
        /// </summary>
        /// <param name="geoLocation"></param>
        /// <param name="Radius"></param>
        /// <param name="ShowHistoric">Defaults to false</param>
        /// <param name="MarkerCap">Defaults to 100. The total number of markers to return.</param>
        /// <returns></returns>
        private FeatureCollection GetMarkers(GeoLocation geoLocation, double Radius, bool ShowHistoric = false, int MarkerCap = 100) {

            var charityMarkers = GetCharityMarkers(geoLocation, Radius, MarkerCap);

            if(charityMarkers.Count() == 0) {

                return new FeatureCollection {
                    Type = "FeatureCollection",
                    Features = new Feature[] { }
                };
            }

            List<Feature> featureMarkers = new List<Feature>();
            if(ShowHistoric == false) {
                // snapshot the time the request was made
                var utcNow = DateTime.UtcNow;
                var threeHoursAgo = utcNow.AddHours(-3);

                foreach(var marker in charityMarkers) {
                    Feature f = marker.ToGeoJsonFeature();
                    var timeSince = marker.TimestampUTC - threeHoursAgo;
                    // this is awkward counting the reverse but uh...
                    // I'll get angry about this later when it bites me in the ass
                    if(timeSince.Hours == 2) {
                        // same hour
                        f.Properties.Size = 1.1;
                    } else if(timeSince.Hours == 1) {
                        // 1 hour ago
                        f.Properties.Size = 0.85;
                    } else if(timeSince.Hours == 0) {
                        // 2 hours
                        f.Properties.Size = 0.75;
                        f.Properties.Opacity = 0.75;
                    } else {
                        // more than 3 (negative timeSince)
                        f.Properties.Size = 0.5;
                        f.Properties.Opacity = 0.5;
                    }
                    featureMarkers.Add(f);
                }
            } else {
                foreach(var marker in charityMarkers) {
                    Feature f = marker.ToGeoJsonFeature();
                    f.Properties.Size = 0.25;
                    featureMarkers.Add(f);
                }
            }

            return new FeatureCollection {
                Type = "FeatureCollection",
                Features = featureMarkers.ToArray()
            };
        }

        /// <summary>
        ///     <para>
        /// Returns full details of all charities around a point, with a radius in meters
        ///     </para>
        /// </summary>
        /// <param name="geoLocation"></param>
        /// <param name="Radius"></param>
        /// <param name="MarkerCap"></param>
        /// <returns></returns>
        private List<CharityMarker> GetCharityMarkers(GeoLocation geoLocation, double Radius, int MarkerCap = 100) {

            List<CharityMarker> DetailedMarkers = new List<CharityMarker>();

            // Get all markers around a point
            var Around = _redis.GeoRadius(_allMarkersKey, geoLocation.Longitude, geoLocation.Latitude, Radius, GeoUnit.Meters)
                .OrderBy(x => x.Distance)
                .Take(MarkerCap);

            // If we don't have any, return an empty list
            if(Around.Count() == 0) {
                return DetailedMarkers;
            }

            // Select all the marker Ids and their distances from the given location
            var MarkerIds = Around
                .Select(x => new {
                    Id = (double)x.Member,
                    Dist = (double)x.Distance // in meters
                })
                .ToDictionary(x => x.Id, x => x.Dist);

            // Select the corresponding charity details from the details key
            IEnumerable<CharityMarker> markers = _redis.HashGet(_entriesMarkersKey, Array.ConvertAll(Around.Select(x => x.Member).ToArray(), item => (RedisValue)item))
                .Select(x => JsonConvert.DeserializeObject<CharityMarker>(x));

            // create a distinct array of charity ids
            long[] DistinctCharityIds = markers.Select(x => x.CharityId)
                .Distinct()
                .ToArray();

            // Get all the styles for each charity
            Dictionary<long, CharityStyle> MarkerStyles = GetCharityMarkerStyles(DistinctCharityIds);

            // Mash the above 2 together and return it
            DetailedMarkers = markers
                .Select(x => {
                    x.Style = MarkerStyles[x.CharityId];
                    x.Distance = MarkerIds[x.CharityId];
                    return x;
                })
                .ToList();

            return DetailedMarkers;
        }

        /// <summary>
        ///     <para>
        /// Runs a batch lookup of charities and returns their respective colours
        ///     </para>
        /// </summary>
        /// <param name="CharityIds"></param>
        private Dictionary<long, CharityStyle> GetCharityMarkerStyles(long[] CharityIds) {
            var charityColourTable = CharityController.GetCharityStyleLookupKey(_settings);
            var markerStyles = _redis.HashGet(charityColourTable, Array.ConvertAll(CharityIds, item => (RedisValue)item))
                .Select(x => JsonConvert.DeserializeObject<CharityStyle>(x))
                .ToDictionary(x => x.CharityId, x => x);
            return markerStyles;
        }

        private Task<List<Charity.Charity>> GetCharities(long[] Charities) {
            var charityController = new CharityController(_redis, _settings);
            return charityController.GetCharitiesByIds(Charities);
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
        /// <summary>
        /// In meters
        /// </summary>
        public double Distance { get; set; }

        [JsonIgnore]
        // Pulled from charities when needed
        public CharityStyle Style { get; set; }

        public Charity.Charity CharityDetails { get; set; }

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
                    MarkerPrimary = Style.PrimaryColour,
                    MarkerSecondary = Style.SecondaryColour,
                    MarkerIcon = Style.Icon,
                    MarkerId = Id,
                    Opacity = 1.0,
                    Size = 1.0
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
        /// Main marker colour (background)
        /// </summary>
        [JsonProperty("Marker-Primary")]
        public string MarkerPrimary { get; set; }
        /// <summary>
        /// Secondary marker colour (border)
        /// </summary>
        [JsonProperty("Marker-Secondary")]
        public string MarkerSecondary { get; set; }
        /// <summary>
        /// SVG icon filename
        /// </summary>
        [JsonProperty("Marker-Icon")]
        public string MarkerIcon { get; set; }
        [JsonProperty("Marker-Id")]
        public long MarkerId { get; set; }
        [JsonProperty("Marker-Opacity")]
        public double Opacity { get; set; }
        /// <summary>
        /// Multiplier, 0.0 - 1.0
        /// </summary>
        [JsonProperty("Marker-Size")]
        public double Size { get; set; }
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
