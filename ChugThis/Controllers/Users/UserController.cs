using Newtonsoft.Json;
using Nulah.ChugThis.Models;
using Nulah.ChugThis.Models.Users;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Controllers.Users {
    public class UserController {
        private readonly IDatabase _redis;
        private readonly AppSettings _settings;
        private readonly string _userKey;
        private const string USER_HASH_PROFILE = "Profile";

        public UserController(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
            _userKey = $"{Settings.ConnectionStrings.Redis.BaseKey}Users";
        }

        /// <summary>
        ///     <para>
        /// STATIC
        ///     </para>
        ///     <para>
        /// Gets a PublicUser profile based on a given Redis key and Redis instance. Used for quick lookups that do not
        /// need anything else.
        ///     </para>
        /// </summary>
        /// <param name="RedisKey"></param>
        /// <param name="Redis"></param>
        /// <returns></returns>
        internal static PublicUser GetUser(string RedisKey, IDatabase Redis) {
            if(Redis.HashExists(RedisKey, USER_HASH_PROFILE)) {

                // Get stored user from the cache, and update the last seen
                PublicUser user = JsonConvert.DeserializeObject<PublicUser>(Redis.HashGet(RedisKey, USER_HASH_PROFILE));
                return user;
            }
            throw new KeyNotFoundException($"Unable to find PublicUser data at: {RedisKey}");
        }

        /// <summary>
        ///     <para>
        /// Removes a given userId from the logged in set
        ///     </para>
        /// </summary>
        /// <param name="RedisKey"></param>
        /// <param name="Redis"></param>
        public void LogUserOut(long UserId) {
            //_redis.SetRemove()
        }

        /// <summary>
        ///     <para>
        /// Returns a users Redis key, based on their User data.
        ///     </para>
        ///     <para>
        /// Called during UserOAuth
        ///     </para>
        /// </summary>
        /// <param name="UserId"></param>
        /// <param name="Provider"></param>
        /// <returns></returns>
        public string GetUserTableKey(User UserData) {
            return $"{_userKey}:{UserData.ProviderShort}-{UserData.Id}";
        }

        /// <summary>
        ///     <para>
        /// Returns a users Redis key, based on their provider and UserId.
        ///     </para>
        ///     <para>
        /// Used when we already have a PublicUser instance for a logged in user. Most likely will never be needed,
        /// as the logged in user context should carry the users Redis key.
        ///     </para>
        /// </summary>
        /// <param name="UserId"></param>
        /// <param name="Provider"></param>
        /// <returns></returns>
        public string GetUserTableKey(PublicUser UserData) {
            return $"{_userKey}:{UserData.ProviderShort}-{UserData.Id}";
        }

        /// <summary>
        ///     <para>
        /// Creates a new user from User data, or returns their existing profile, updating their last login time.
        ///     </para>
        /// </summary>
        /// <param name="UserData"></param>
        /// <returns></returns>
        public PublicUser GetOrCreateUser(User UserData) {

            var userKey = GetUserTableKey(UserData);
            PublicUser user = GetUserFromCache(userKey);

            if(user != null) {
                UpdateLastSeen(user);
            } else {
                // Create a new user record
                user = new PublicUser() {
                    Id = UserData.Id,
                    LastSeenUTC = DateTime.UtcNow,
                    Name = UserData.Name,
                    Provider = UserData.Provider,
                    ProviderShort = UserData.ProviderShort,
                    isExpressMode = false,
                    Zoom = new Models.Maps.ZoomOptions(), // Will have defaults set on creation
                    Marker = new Models.Maps.MarkerOptions(), // Will have defaults set on creation.
                    Preferences = new Preferences()
                };
            }

            // Add whatever data we have to the cache
            CreateOrUpdateCachedUser(user);

            return user;
        }

        /// <summary>
        ///     <para>
        /// Adds a new markerId to a users marker list.
        ///     </para>
        ///     <para>
        /// If the marker has already been added nothing will happen. Creates a new set if one does not exist.
        ///     </para>
        /// </summary>
        /// <param name="CharityName"></param>
        /// <param name="MarkerId"></param>
        public void AddMarkerToUser(string UserId, long MarkerId) {

            var userMarkerHash = $"{_userKey}:{UserId}";
            List<long> MarkerSet;

            // check to see if the hash exists
            if(_redis.HashExists(userMarkerHash, "Markers")) {
                // deserialize the existing set
                MarkerSet = JsonConvert.DeserializeObject<List<long>>(_redis.HashGet(userMarkerHash, "Markers"));
                // if the set doesn't contain the MarkerId, add it
                if(!MarkerSet.Contains(MarkerId)) {
                    MarkerSet.Add(MarkerId);
                }
            } else {
                // Create a new marker set from the MarkerId
                MarkerSet = new List<long> { MarkerId };
            }
            // add the marker set to the hash
            _redis.HashSet(userMarkerHash, "Markers", JsonConvert.SerializeObject(MarkerSet));
        }

        private PublicUser GetUserFromCache(string UserKey) {
            if(_redis.HashExists(UserKey, USER_HASH_PROFILE)) {

                // Get stored user from the cache, and update the last seen
                PublicUser user = JsonConvert.DeserializeObject<PublicUser>(_redis.HashGet(UserKey, USER_HASH_PROFILE));
                return user;
            }

            return null;
        }

        /// <summary>
        ///     <para>
        /// Updates the last seen time for a cached user
        ///     </para>
        /// </summary>
        /// <param name="User"></param>
        private void UpdateLastSeen(PublicUser User) {
            if(User == null) {
                throw new ArgumentNullException("PublicUser given cannot be null.");
            }

            User.LastSeenUTC = DateTime.UtcNow;
        }

        /// <summary>
        ///     <para>
        /// Adds a PublicUser to the cache. 
        ///     </para>
        /// </summary>
        /// <param name="User"></param>
        /// <returns></returns>
        private PublicUser CreateOrUpdateCachedUser(PublicUser User) {
            if(User == null) {
                throw new ArgumentNullException("PublicUser given cannot be null.");
            }

            try {
                _redis.HashSet(GetUserTableKey(User), USER_HASH_PROFILE, JsonConvert.SerializeObject(User));
            } catch(Exception e) {
                throw new Exception("Redis Exception on creating user entry.", e);
            }

            return User;
        }
    }
}
