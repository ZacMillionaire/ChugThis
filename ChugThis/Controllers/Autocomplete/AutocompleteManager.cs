using Nulah.ChugThis.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Controllers.Autocomplete {
    public class AutocompleteManager {
        private readonly IDatabase _redis;
        private readonly AppSettings _settings;

        private readonly string _autocompleteBaseKey;

        /// <summary>
        /// Used to set the length of autocomplete key names.
        /// Represents the upper limit of characters to take from the start of a given string.
        /// </summary>
        private const int KEY_LENGTH = 3;

        public AutocompleteManager(IDatabase Redis, AppSettings Settings) {
            _redis = Redis;
            _settings = Settings;

            _autocompleteBaseKey = $"{Settings.ConnectionStrings.Redis.BaseKey}AutoComplete";
        }

        /// <summary>
        ///     <para>
        /// Takes a string and adds it to an autocomplete index
        ///     </para>
        /// </summary>
        /// <param name="NewEntry">The string to convert to autocomplete</param>
        /// <param name="Index">Index is the name of the type being autocomplete. For grouping.</param>
        public void AddWordToAutoComplete(string NewEntry, string Index) {
            var targetAutoCompleteKey = $"{_autocompleteBaseKey}:{Index}";
            // used for rebuilding the index if an entry is renamed or removed later on.
            var rebuildList = $"{targetAutoCompleteKey}:RebuildList";

            _redis.SetAdd(rebuildList, NewEntry);

            var Keys = NewEntry.ToLower().Take(KEY_LENGTH);

            string runningKey = "";

            foreach(var k in Keys) {
                runningKey += k;
                var thisKey = $"{targetAutoCompleteKey}:{runningKey}";
                _redis.SortedSetAdd(thisKey, NewEntry, 0);
            }

            /*
            create a bunch of sorted sets in the form of {basekey:}AutoComplete:{Index}:[...]
            where [...]
            is a building list of the first 4 letters of the NewEntry added
            so for a given charity GreenPeace, the sorted sets would produce:
            {basekey:}AutoComplete:{Index}:g
            {basekey:}AutoComplete:{Index}:gr
            {basekey:}AutoComplete:{Index}:gre
            {basekey:}AutoComplete:{Index}:gree
            where the charity is lowercase, with spaces removed.
            The word "GreenPeace" will be added to each set.

            Later, if we have a charity called Groobers, we already have the indexes for g and gr,
            so we add Groobers to those, and create 2 more indexes,
            {basekey:}AutoComplete:{Index}:gro
            {basekey:}AutoComplete:{Index}:groo
            with the word Groober added to them.

            I'm not entirely sure if I want this many indexes, so I might just leave it at 2 letters to start,
            then if enough charities are added I'll extend it by increasing the _keyLength value.

            */
        }

        /// <summary>
        ///     <para>
        /// Returns a list based on a number of characters from the start of the Term against a given Index. Ordered Descending by rank, then lexigraphically.
        ///     </para>
        ///     <para>
        /// Returns an empty list on a null Term or no results
        ///     </para>
        /// </summary>
        /// <param name="Term"></param>
        /// <param name="Index"></param>
        /// <returns></returns>
        public List<string> GetSuggestions(string Term, string Index) {

            if(Term == null) {
                return new List<string>();
            }

            string searchKey = $"{_autocompleteBaseKey}:{Index}";
            Term = Term.ToLower();
            // Trim the term to the length of autocomplete keys if longer
            if(Term.Length > KEY_LENGTH) {
                searchKey = $"{searchKey}:{Term.Substring(0, KEY_LENGTH)}";
            } else {
                searchKey = $"{searchKey}:{Term}";
            }


            var results = _redis.SortedSetRangeByRank(searchKey, 0, -1, Order.Descending);
            var suggestions = results.Select(x => (string)x).ToList();

            return suggestions;

        }

        /// <summary>
        ///     <para>
        /// Increases the rank of a given item based on the Term that resulted in it being selected
        ///     </para>
        /// </summary>
        /// <param name="Item"></param>
        /// <param name="Term"></param>
        /// <param name="Index"></param>
        public void IncreaseAutocompleteRank(string Item, string Term, string Index) {

            if(Term == null || Item == null) {
                return;
            }
            Term = Term.ToLower();

            string searchKey = $"{_autocompleteBaseKey}:{Index}:{Term}";

            if(_redis.KeyExists(searchKey)) {
                _redis.SortedSetIncrement(searchKey, Item, 1);
            }
        }

        public void RebuildAutoComplete(string Index) {
            var targetAutoCompleteKey = $"{_autocompleteBaseKey}:{Index}";
            // used for rebuilding the index if an entry is renamed or removed later on.
            var rebuildList = $"{targetAutoCompleteKey}:RebuildList";

        }

    }
}
