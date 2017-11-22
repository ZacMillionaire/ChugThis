using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models.Maps {
    public class ZoomOptions {
        public int Starting { get; set; }
        public int Desktop { get; set; }
        public int Mobile { get; set; }

        public ZoomOptions() {
            Starting = 16;
            Desktop = 19;
            Mobile = 18;
        }
    }
}
