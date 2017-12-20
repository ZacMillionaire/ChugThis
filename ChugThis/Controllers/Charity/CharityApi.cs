using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Nulah.ChugThis.Models;
using Nulah.ChugThis.Controllers.Autocomplete;
using Nulah.ChugThis.Filters;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Nulah.ChugThis.Controllers.Charity {
    public class CharityApi : Controller {

        private readonly IDatabase _redis;
        private readonly AppSettings _settings;

        public CharityApi(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;
        }

        [HttpGet]
        [Route("~/Api/GetCharitySuggestion")]
        public IEnumerable<string> Get(string Name) {
            var AC = new AutocompleteManager(_redis, _settings);
            return AC.GetSuggestions(Name, "Charity");
        }

        [HttpGet]
        [Route("~/Api/BumpCharitySuggestion")]
        [UserFilter(RequiredLoggedInState: true)]
        public void Bump(string Name, string Term) {
            var AC = new AutocompleteManager(_redis, _settings);
            AC.IncreaseAutocompleteRank(Name, Term, "Charity");
        }
    }
}
