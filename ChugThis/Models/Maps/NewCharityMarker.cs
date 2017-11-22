using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nulah.ChugThis.Models.Geo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models.Maps {

    /// <summary>
    /// For form data
    /// </summary>
    [ModelBinder(BinderType = typeof(NewCharityEntityBinder))]
    public class NewCharityMarker {
        public GeoLocation Location { get; set; }
        /// <summary>
        /// Name of the charity
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// What they were doing
        /// </summary>
        public string[] Doing { get; set; }
    }

    public class NewCharityEntityBinder : IModelBinder {

        public Task BindModelAsync(ModelBindingContext bindingContext) {
            if(bindingContext == null) {
                throw new ArgumentNullException(nameof(bindingContext));
            }
            GeoLocation geoLoc;

            string[] locationString = bindingContext.ValueProvider
                .GetValue("Location")
                .FirstValue
                .Split(' ');

            double Longitude = double.Parse(locationString[0]);
            double Latitude = double.Parse(locationString[1]);

            try {
                geoLoc = new GeoLocation(
                    double.Parse(locationString[0]),
                    double.Parse(locationString[1])
                );
            } catch(Exception e) {
                throw new Exception("Unable to parse geolocation data", e);
            }

            string CharityName = bindingContext.ValueProvider
                .GetValue("Name")
                .FirstValue;
            string[] Doing = bindingContext.ValueProvider
                .GetValue("Doing")
                .Values;

            bindingContext.Result = ModelBindingResult.Success(new NewCharityMarker() {
                Location = geoLoc,
                Name = CharityName,
                Doing = Doing
            });

            return Task.CompletedTask;
        }
    }

}
