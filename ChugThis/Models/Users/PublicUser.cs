using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models.Users {
    public class PublicUser {
        public string Id { get; set; }
        public string Provider { get; set; }
        public string Name { get; set; }
        public bool isLoggedIn { get; set; }

        public PublicUser() {
            isLoggedIn = false;
        }
    }
}
