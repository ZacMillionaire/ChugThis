using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models.Users {
    public class Preferences {
        public bool AutoLocate { get; set; }
        public Preferences() {
            AutoLocate = false;
        }
    }
}
