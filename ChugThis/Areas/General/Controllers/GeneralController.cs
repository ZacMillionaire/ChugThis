using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Nulah.ChugThis.Areas.General.Controllers {
    [Area("General")]
    public class GeneralController : Controller {
        [HttpGet]
        [Route("~/About")]
        public IActionResult About() {
            return View();
        }

        [HttpGet]
        [Route("~/About/Sponsor")]
        public IActionResult AboutSponsor() {
            return View();
        }
    }
}
