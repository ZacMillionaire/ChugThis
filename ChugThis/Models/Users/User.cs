using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models.Users {
    public class User {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Provider { get; set; }
        public string ProviderShort { get; set; }
    }
}
