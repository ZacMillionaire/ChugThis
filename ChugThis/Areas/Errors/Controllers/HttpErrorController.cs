using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Areas.Errors.Controllers {
    [Area("Errors")]
    public class HttpErrorController : Controller {

        [Route("~/Error/500")]
        public IActionResult ServerError() {
            return View();
        }

        [Route("~/Error/404")]
        public IActionResult FileNotFound() {
            // If the ResourceType is set on Items, chances are we have a 404 on a file resource. So we return a JSON response instead.
            // Why? Probably because I don't want to return an entire HTML document on a file not found.
            // Thats stupid as fuck, no one needs to have that in a file not found response.
            if(HttpContext.Items.ContainsKey("ResourceType") && HttpContext.Items["ResourceType"].ToString() == "file") {
                return Json(new {
                    StatusCode = 404,
                    ErrorMessage = $"Resource not found:{HttpContext.Items["PreviousRequestPath"]}"
                });
            } else {
                // The ScreamingInterception middleware will populate this item
                ViewBag.RequestedPath = HttpContext.Items["PreviousRequestPath"];
                return View();
            }
        }

        [Route("~/Error/{StatusCode}")]
        public IActionResult UnhandledError(int StatusCode) {
            ViewBag.StatusCode = StatusCode;
            return View();
        }
    }
}
