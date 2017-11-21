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
        public bool isLoggedIn { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        public DateTime LastSeenUTC { get; set; }

        public PublicUser() {
            isLoggedIn = false;
        }
    }
}
