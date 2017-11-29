using Newtonsoft.Json;
using Nulah.ChugThis.Models;
using Nulah.ChugThis.Models.Geo;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Controllers.Charity {
    public class CharityController {

        private readonly IDatabase _redis;
        private readonly AppSettings _settings;
        private readonly string _charityBaseKey;
        private readonly string _charityStyleLookupKey;
        private readonly string _charityIndexKey;

        private const string CHARITY_ID_COUNTER = "ID:Charity";
        private const string CHARITY_INDEX = "Index:Charity";
        private const string CHARITY_MARKER_STYLE_TABLE = "Charity:MarkerStyles";

        public CharityController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;

            _charityBaseKey = $"{Settings.ConnectionStrings.Redis.BaseKey}Charity";
            _charityStyleLookupKey = $"{Settings.ConnectionStrings.Redis.BaseKey}{CHARITY_MARKER_STYLE_TABLE}";
            _charityIndexKey = $"{Settings.ConnectionStrings.Redis.BaseKey}{CHARITY_INDEX}";
        }

        public string CharityBaseKey
        {
            get
            {
                return _charityBaseKey;
            }
        }

        public static string GetCharityStyleLookupKey(AppSettings Settings) {
            return $"{Settings.ConnectionStrings.Redis.BaseKey}{CHARITY_MARKER_STYLE_TABLE}";
        }

        /// <summary>
        ///     <para>
        /// Creates a new Charity object, or returns an existing record if a condition race occurs where 1 already creates a new entry.
        ///     </para>
        /// </summary>
        /// <param name="CharityName"></param>
        /// <returns></returns>
        public Charity CreateCharityEntry(string CharityName) {
            // The race condition is unlikely, but hey, I get to pretend to know a thing.
            // This validates my degree.

            string charityNameKey = CharityName.ToLower();
            charityNameKey = System.Text.RegularExpressions.Regex.Replace(charityNameKey, @"[^\w\d]+", "");

            var charityKey = $"{_charityBaseKey}:{charityNameKey}";

            // Doublecheck to make sure it didn't get added
            if(_redis.HashExists(charityKey, "Profile")) {
                return JsonConvert.DeserializeObject<Charity>(_redis.HashGet(charityKey, "Profile"));
            } else {
                long NewCharityId = GetNextCharityId();
                Charity newCharityEntry = new Charity {
                    Id = NewCharityId,
                    Name = CharityName,
                    Style = new CharityStyle {
                        CharityId = NewCharityId,
                        PrimaryColour = GetHexColourFromCharityName(CharityName)
                    },
                    isNew = true
                };

                CreateOrUpdateCharityStyle(NewCharityId, newCharityEntry.Style);

                _redis.HashSet(charityKey, "Profile", newCharityEntry.ToString());
                AddCharityToIndex(NewCharityId, charityNameKey);
                return newCharityEntry;
            }
        }

        /// <summary>
        ///     <para>
        /// Adds a charity name to the index
        ///     </para>
        /// </summary>
        /// <param name="CharityId"></param>
        /// <param name="CharityName"></param>
        private void AddCharityToIndex(long CharityId, string CharityName) {
            // this is kind of a dumb way to do this, the charity Id could be the charities key,
            // however, doing it the current way ensures that duplicates don't exist.
            _redis.HashSet(_charityIndexKey, CharityId, CharityName);
        }

        private void RemoveCharityFromIndex(long CharityId) {
            throw new NotImplementedException("Removing from charity index not supported");
        }

        /// <summary>
        ///     <para>
        /// Creates (or updates if a hash for a charityId already exists) a charity style entry for a given charity Id, then returns the modified entry.
        ///     </para>
        /// </summary>
        /// <param name="CharityId"></param>
        /// <param name="NewStyle"></param>
        /// <returns></returns>
        private CharityStyle CreateOrUpdateCharityStyle(long CharityId, CharityStyle NewStyle) {
            _redis.HashSet(_charityStyleLookupKey, CharityId, JsonConvert.SerializeObject(NewStyle));
            return JsonConvert.DeserializeObject<CharityStyle>(_redis.HashGet(_charityStyleLookupKey, CharityId));
        }

        /// <summary>
        ///     <para>
        /// Returns a valid RGB hex colour based on a charity name. Used so new charities have some colour to identify off on the map when first created.
        ///     </para>
        /// </summary>
        /// <param name="CharityName"></param>
        /// <returns></returns>
        private string GetHexColourFromCharityName(string CharityName) {
            var charityBytes = CharityName.GetHashCode() & 0x00FFFFFF;
            return charityBytes.ToString("x2");
        }

        /// <summary>
        ///     <para>
        /// Returns the next charity ID
        ///     </para>
        ///     <para>
        /// As per Redis, if the key does not exist, it is created at 0, then incremented, then returns 1 as the next value
        ///     </para>
        /// </summary>
        /// <returns></returns>
        public long GetNextCharityId() {
            var markerKey = $"{_settings.ConnectionStrings.Redis.BaseKey}{CHARITY_ID_COUNTER}";
            return _redis.StringIncrement(markerKey);
        }

        /// <summary>
        ///     <para>
        /// Creates a new charity by name if it doesn't exist.
        ///     </para>
        ///     <para>
        /// Returns an existing charity if found.
        ///     </para>
        ///     <para>
        /// New charities will be given a random colour hex and marked as isNew for later updating.
        ///     </para>
        /// </summary>
        /// <param name="CharityName"></param>
        /// <returns></returns>
        public Charity GetOrCreateCharityByName(string CharityName) {

            var charityKey = $"{_charityBaseKey}:{CharityName}";
            Charity charityProfile;

            if(_redis.HashExists(charityKey, "Profile")) {
                charityProfile = JsonConvert.DeserializeObject<Charity>(_redis.HashGet(charityKey, "Profile"));
            } else {
                charityProfile = CreateCharityEntry(CharityName);
            }

            return charityProfile;
        }

        /// <summary>
        ///     <para>
        /// Adds a new markerId to a charities marker list.
        ///     </para>
        ///     <para>
        /// If the marker has already been added nothing will happen. Creates a new set if one does not exist.
        ///     </para>
        /// </summary>
        /// <param name="CharityName"></param>
        /// <param name="MarkerId"></param>
        public void AddMarkerToCharity(string CharityName, long MarkerId) {

            var charityKey = $"{CharityBaseKey}:{CharityName}";
            List<long> MarkerSet;

            // check to see if the hash exists
            if(_redis.HashExists(charityKey, "Markers")) {
                // deserialize the existing set
                MarkerSet = JsonConvert.DeserializeObject<List<long>>(_redis.HashGet(charityKey, "Markers"));
                // if the set doesn't contain the MarkerId, add it
                if(!MarkerSet.Contains(MarkerId)) {
                    MarkerSet.Add(MarkerId);
                }
            } else {
                // Create a new marker set from the MarkerId
                MarkerSet = new List<long> { MarkerId };
            }
            // add the marker set to the hash
            _redis.HashSet(charityKey, "Markers", JsonConvert.SerializeObject(MarkerSet));
        }

    }

    public class Charity {
        public long Id { get; set; }
        /// <summary>
        /// Charities name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// URL to the charities home page. I'm not a monster enough to deny reaching out to them.
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// Short description about what the charity "does"
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// Indicates if the charity was recently created, and lacks a URL or details
        /// </summary>
#pragma warning disable IDE1006 // Naming Styles
        public bool isNew { get; set; }
#pragma warning restore IDE1006 // Naming Styles

        /// <summary>
        /// Colours and icons to use to distinguish this charity on maps
        /// </summary>
        public CharityStyle Style { get; set; }

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
    }

    public class CharityStyle {
        /// <summary>
        /// Background colour for their marker on the map view in most cases
        /// </summary>
        public string PrimaryColour { get; set; }
        /// <summary>
        /// Accent colour, either border on map view, or bottom border in details view.
        /// </summary>
        public string SecondaryColour { get; set; }
        /// <summary>
        /// Will eventually be a link to an SVG file for the charity.
        /// </summary>
        public string Icon { get; set; }
        public long CharityId { get; set; }
    }
}
