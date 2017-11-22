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

        private const string CHARITY_ID_COUNTER = "ID:Charity";

        public CharityController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
            _charityBaseKey = $"{Settings.ConnectionStrings.Redis.BaseKey}Charity";
        }

        public string CharityBaseKey
        {
            get
            {
                return _charityBaseKey;
            }
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

            var charityKey = $"{_charityBaseKey}:{CharityName}";

            // Doublecheck to make sure it didn't get added
            if(_redis.HashExists(charityKey, "Profile")) {
                return JsonConvert.DeserializeObject<Charity>(_redis.HashGet(charityKey, "Profile"));
            } else {
                Charity newCharityEntry = new Charity {
                    Id = GetNextCharityId(),
                    Name = CharityName,
                    MarkerColour = GetHexColourFromCharityName(CharityName),
                    isNew = true
                };
                _redis.HashSet(charityKey, "Profile", newCharityEntry.ToString());
                return newCharityEntry;
            }
        }

        /// <summary>
        ///     <para>
        /// Returns a valid RGB hex colour based on a charity name. Used so new charities have some colour to identify off on the map when first created.
        ///     </para>
        /// </summary>
        /// <param name="CharityName"></param>
        /// <returns></returns>
        private string GetHexColourFromCharityName(string CharityName) {
            // Lets be lazy and just take the first 3 letters of the charity name and get their bytes
            var charityBytes = Encoding.ASCII.GetBytes(CharityName).Take(3);
            StringBuilder sb = new StringBuilder();

            // Then we'll just turn each one into a hex
            foreach(var b in charityBytes) {
                sb.Append(b.ToString("x2"));
            }

            // and return the resulting string.
            // Alt: How to turn 3 chars into 6
            return sb.ToString();
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
        public bool isNew { get; set; }

        /// <summary>
        /// RGB colour hex
        /// </summary>
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
    }
}
