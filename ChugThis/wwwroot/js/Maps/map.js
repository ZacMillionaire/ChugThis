var ChugMap = function (ApiKey, Options) {

    if (Options === undefined) {
        console.warn("No options given, using defaults");
        _Options = {
            Container: 'mapbox-container',
            Zoom: {
                StartZoom: 16,
                Desktop: 19,
                Mobile: 18
            },
            MarkerSize: {
                Add: 25,
                CharityBase: 20
            },
            UserPreferences: {
                // it sucks to have this on by default, but it's to draw attention to user preferences.
                // Users annoyed by the extra wait will change this pretty quickly.
                AutoLocate: false
            },
            FormTarget: "#add-new-marker-form"
        };
    }

    var __self = this;
    var _MapBoxApiKey = ApiKey;
    var _UserLocation = null;
    var _Options = Options;
    var _TempCharityMarker = null;
    var _TouchDrag = false;
    var _LoadedCharityMarkers = [];
    var _NewMarkerForm = document.querySelector(_Options.FormTarget);
    var _Scale = null;
    var _MobileMode = false; // used to prevent click events on mobile
    var _MarkerZoom = _Options.Zoom.Desktop; // default to desktop zoom
    var _MapBox = null;
    var _MapLoaded = false;

    /*
    // rough bounding box for Australia
    var _MapBounds = [
        [112.7197265625, -43.7869583731], // SW
        [153.9404296875, -10.5580220134], // NE
    ];
    */

    // Define some time saving stuff later because I don't like
    // having to write bullshit when I can just call a method
    Object.defineProperties(Array.prototype,
        {
            "Contains": {
                enumerable: false,
                value: function (v) {
                    return this.indexOf(v) !== -1;
                }
            },
            "Add": {
                enumerable: false,
                value: function (v) {
                    // If I use this somewhere else, I'll probably have to make sure that
                    // added elements past the first, match the object type of the first index.
                    // for now I'm just using it to store ints so I really don't care.
                    if (this.indexOf(v) === -1) {
                        this.push(v);
                    }
                }
            },
            "Remove": {
                enumerable: false,
                value: function (v) {
                    var valueIndex = this.indexOf(v);
                    if (valueIndex !== -1) {
                        this.splice(valueIndex, 1);
                    }
                }
            },
            "Difference": {
                enumerable: false,
                value: function (NewSet) {
                    // returns an array of elements that were not found in the new set
                    // eg, they were unique to the old set
                    var oldSet = this.Clone();
                    for (var i in NewSet) {
                        if (oldSet.Contains(NewSet[i])) {
                            oldSet.Remove(NewSet[i]);
                        }
                    }
                    return oldSet;
                }
            },
            "Clone": {
                enumerable: false,
                value: function () {
                    return JSON.parse(JSON.stringify(this));
                }
            }
        }
    );

    function ReturnCoordObject(Long, Lat) {
        return {
            Latitude: Lat,
            Longitude: Long
        };
    }

    function RenderMap(StartingPosition) {

        PageBusyOverlay.Show("Loading Map Data From MapBox...");

        if (_MapBoxApiKey === null) {
            PageBusyOverlay.Error("Error loading map. API Key missing.");
            return;
        }

        _UserLocation = ReturnCoordObject(StartingPosition.coords.longitude, StartingPosition.coords.latitude);

        mapboxgl.accessToken = _MapBoxApiKey;

        _MapBox = new mapboxgl.Map({
            container: _Options.Container, // container id
            style: 'mapbox://styles/mapbox/streets-v9',
            center: [_UserLocation.Longitude, _UserLocation.Latitude], // starting position, [long,lat]
            zoom: _Options.Zoom.StartZoom, // starting zoom
            //maxBounds: _MapBounds
        });


        var geoLocationControl = new mapboxgl.GeolocateControl({
            positionOptions: {
                enableHighAccuracy: false,
                maximumAge: 1000
            },
            fitBoundsOptions: {
                maxZoom: 15
            },
            trackUserLocation: true
        });
        _MapBox.addControl(geoLocationControl);


        _MapBox.on("load", function () {

            _MapLoaded = true;

            if (_Options.UserPreferences.AutoLocate === true) {
                // If it loots stupid...
                var _singleLocEvent = false;

                function oneOfftrackuserlocationstart(Event) {
                    PageBusyOverlay.Show("Locating you... (You can turn this off in your settings)");
                }

                function oneOffGeolocateEvent(Event) {

                    // ...It's probably a stupid fix for mobile development
                    if (_singleLocEvent === true) {
                        return;
                    }
                    // Prevent this from firing ever again
                    _singleLocEvent = true;

                    _MapBox.zoomTo(_Options.Zoom.StartZoom); // because lol MaxZoom means fucking nothing if you want to stay close
                    PageBusyOverlay.Show("Finding hot chuggers in your area...");
                    // Wait until data is loaded the first time
                    RenderCharities(_UserLocation).then(function () {
                        // hide the overlay
                        PageBusyOverlay.Hide();
                        // unbind location control events. Don't care anymore.
                        // This makes sure that no stupid cutesy first time load shit happens again
                        geoLocationControl.off("geolocate", oneOffGeolocateEvent);
                        geoLocationControl.off("trackuserlocationstart", oneOfftrackuserlocationstart);
                    });
                }

                // User Location Tracking (geospatially, not analytics)
                //AddUserLocation(_MapBox);

                //console.log(_MapBox);

                geoLocationControl.on("trackuserlocationstart", oneOfftrackuserlocationstart);
                geoLocationControl.on("geolocate", oneOffGeolocateEvent);

                geoLocationControl._onClickGeolocate();
            } else {
                PageBusyOverlay.Show("Finding hot chuggers in your area...");
                // Wait until data is loaded the first time
                RenderCharities(_UserLocation).then(function () {
                    // hide the overlay
                    PageBusyOverlay.Hide();
                });
            }

            try {
                // This is kind of horrible, but it's done so if a user isn't logged in when they tap to add a marker,
                // we redirect them to the form with where they tapped so they can add the marker they had planned with minimal fuss.
                var queryParams = UriHelper.GetQueryParams(window.location.search);
                if (queryParams.Count > 0) {
                    PageBusyOverlay.Show("Putting you back to where you were...");
                    var lnglat = queryParams["lnglat"].split(",");
                    var zoom = queryParams["zoom"]
                    console.log(lnglat);
                    var queryStringGeo = ReturnCoordObject(lnglat[0], lnglat[1]);
                    AddCharityMarker(queryStringGeo, "PageLoad", zoom);

                }
            } catch (e) {
                console.error(e);
            }
        });

        _MapBox.on("error", function (e) {
            PageBusyOverlay.Error("Error loading map. API Key invalid or expired. Sorry about that.");
        });

        _MapBox.on("dragend", function (e) {
            if (_MapLoaded) {
                var mapcenter = _MapBox.getCenter();
                RenderCharities(ReturnCoordObject(mapcenter.lng, mapcenter.lat));
            }
        });
        _MapBox.on("zoomend", function (e) {
            if (_MapLoaded) {
                var mapcenter = _MapBox.getCenter();
                RenderCharities(ReturnCoordObject(mapcenter.lng, mapcenter.lat));
            }
        });

        // Trigger the add marker
        _MapBox.on("click", function (e) {
            if (_MobileMode === false) {
                AddCharityMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent, _Options.Zoom.Desktop);
            } else {
                // stupid statement to make marker movement work on mobile, because apparently touchend does fuck knows what to mapbox and
                // breaks .flyTo
                // Fuck knows
                AddCharityMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent, _Options.Zoom.Mobile);
            }
        });
        _MapBox.on("touchstart", function (e) {
            _TouchDrag = false;
            // If we're getting a touch start, we're on mobile, so set mobile mode to true to prevent map click events
            if (_MobileMode === false) {
                _MobileMode = true;
                _MarkerZoom = _Options.Zoom.Mobile;
            }
        });
        /*
        // Trigger the add marker for mobile
        // lol no apparently this won't fucking work because: lol who knows
        _MapBox.on("touchend", function (e) {
            if (_TouchDrag === false) {
                AddCharityMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent);
            }
        });
        */
        // disable tap events if a drag occurs (user is probably panning or pinch zooming, not tapping)
        _MapBox.on("touchmove", function (e) {
            _TouchDrag = true;
        });

    }

    function RenderCharities(GeoLoc) {

        // fuck this is messy
        return new Promise(function (resolve, reject) {
            var bbox = _MapBox.getContainer().getBoundingClientRect();
            var width = bbox.width;
            var height = bbox.height;

            var topLeft = _MapBox.unproject([0, 0]);
            var bottomRight = _MapBox.unproject([width, height]);

            var diagonalDistance = calcCrow(topLeft.lat, topLeft.lng, bottomRight.lat, bottomRight.lng);

            // Shamelessly stolen from SnackOverflow until I have time to rewrite it
            // https://stackoverflow.com/questions/18883601/function-to-calculate-distance-between-two-coordinates-shows-wrong
            //This function takes in latitude and longitude of two location and returns the distance between them as the crow flies (in km * 1000 (meters))
            function calcCrow(lat1, lon1, lat2, lon2) {
                var R = 6371; // km
                var dLat = toRad(lat2 - lat1);
                var dLon = toRad(lon2 - lon1);
                lat1 = toRad(lat1);
                lat2 = toRad(lat2);

                var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                    Math.sin(dLon / 2) * Math.sin(dLon / 2) * Math.cos(lat1) * Math.cos(lat2);
                var c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
                var d = R * c;
                return d * 1000;
            }

            // Converts numeric degrees to radians
            function toRad(Value) {
                return Value * Math.PI / 180;
            }


            //console.log("Rendering Charities around", GeoLoc, "with radius", (diagonalDistance / 4) + "m");


            function MoveToCharityMarker(Event, Marker) {
                Event.preventDefault();

                DeactivateAddMarkerForm();

                LoadCharityDetailsFromLocation(ReturnCoordObject(Marker.geometry.coordinates[0], Marker.geometry.coordinates[1]), 50).then(function (details) {
                    RenderDetailList(details);
                });

                //var infobox = document.getElementById("info");
                //infobox.innerHTML = JSON.stringify(Marker);
                try {
                    _MapBox.flyTo({ center: Marker.geometry.coordinates, zoom: _MarkerZoom });
                } catch (ex) {
                    console.log(ex);
                    //infobox.innerHTML = ex;
                }
                //Event.stopPropagation(); // prevent any other things from firing
            }

            GetCharitiesNearLocation(GeoLoc, diagonalDistance).then(function (GeoJsonResult) {

                //console.log("Promise complete", GeoJsonResult);

                // remove all previously loaded markers once new data has loaded
                _LoadedCharityMarkers.forEach(function (marker) {
                    marker.remove();
                });

                _LoadedCharityMarkers = [];

                GeoJsonResult.features.forEach(function (marker) {
                    // create a HTML element for each feature
                    var el = document.createElement('div');
                    el.className = 'charity-marker';
                    if (marker.properties["Marker-Size"] > 1) {
                        el.classList.add("recent");
                    }
                    el.style.backgroundColor = "#" + marker.properties["Marker-Primary"];
                    if (marker.properties["Marker-Secondary"] !== null) {
                        el.style.borderColor = "#" + marker.properties["Marker-Secondary"];
                    }
                    el.style.width = (_Options.MarkerSize.CharityBase * marker.properties["Marker-Size"]) + "px";
                    el.style.height = (_Options.MarkerSize.CharityBase * marker.properties["Marker-Size"]) + "px";
                    el.style.borderRadius = (_Options.MarkerSize.CharityBase * marker.properties["Marker-Size"]) + "px";
                    el.style.opacity = (marker.properties["Marker-Opacity"]);

                    // make a marker for each feature and add to the map
                    var m = new mapboxgl.Marker(el)
                        .setLngLat(marker.geometry.coordinates)
                        .addTo(_MapBox);

                    el.addEventListener("click", function (e) {
                        MoveToCharityMarker(e, marker);
                    });

                    el.addEventListener("touchstart", function (e) {
                        _TouchDrag = false;
                    });
                    // Trigger the add marker for mobile
                    el.addEventListener("touchend", function (e) {
                        if (_TouchDrag === false) {
                            MoveToCharityMarker(e, marker);
                        }
                    });

                    // disable tap events if a drag occurs (user is probably panning or pinch zooming, not tapping)
                    el.addEventListener("touchmove", function (e) {
                        _TouchDrag = true;
                    });


                    _LoadedCharityMarkers.Add(m);
                });

                // HEY HOW ABOUT NESTED PROMISES
                resolve(true);
            });
        }); // end of promise
    }

    function AddCharityMarker(GeoObject, OriginalEvent, Zoom) {

        // clear out the marker details list if previously filled
        UnrenderDetailList();

        // If the url has a lnglat query string param it means the user had attempted to add a marker but wasn't logged in.
        // So lets say they did if the Original Event is the string PageLoad and treat it as if they had clicked the map.
        if (OriginalEvent !== "PageLoad") {
            // Do nothing if the event came from anything other than the map canvas (eg the user clicked on the marker they already placed)
            if (OriginalEvent.target.tagName !== "CANVAS") {
                return;
            }
        }

        // remove the previously added temp marker
        if (_TempCharityMarker !== null) {
            _TempCharityMarker.remove();
        }

        var el = document.createElement('div');
        el.className = 'add-charity-marker';

        // TODO: tapping the add marker marker again should cancel it

        _TempCharityMarker = new mapboxgl.Marker(el)
            .setLngLat([GeoObject.Longitude, GeoObject.Latitude])
            .addTo(_MapBox);

        console.log("adding Charity", GeoObject);
        _MapBox.flyTo({ center: [GeoObject.Longitude, GeoObject.Latitude], zoom: Zoom });

        ActivateAddMarkerForm(GeoObject, Zoom);
    }

    function ActivateAddMarkerForm(Geolocation, Zoom) {
        // Show the new marker form
        _NewMarkerForm.classList.remove("is-hidden");
        // If the user is logged in, we'll have a form loaded, so we'll prefill the long/lat
        if (_NewMarkerForm.querySelector("form") !== null) {
            var locationDisplay = _NewMarkerForm.querySelector("#Location-Display");
            var locationField = _NewMarkerForm.querySelector("#Charity-Location");
            var charityName = _NewMarkerForm.querySelector("#Charity-Name");
            locationDisplay.value = Geolocation.Longitude + " " + Geolocation.Latitude;
            locationField.value = Geolocation.Longitude + " " + Geolocation.Latitude;

            BindOnce(charityName, "keyup", "Autocomplete", AutoCompleteCharity);

            InterceptFormSubmission();
        } else {
            var loginButton = _NewMarkerForm.querySelector("#new-marker-login");

            var redirectParams = UriHelper.GetQueryParams()
                .Add("lnglat", Geolocation.Longitude + "," + Geolocation.Latitude)
                .Add("zoom", Zoom);
            var originalHref = UriHelper.GetQueryParams(loginButton.href);

            originalHref.Add("RedirectUri", "/" + redirectParams.UriStringRaw());
            console.log(originalHref, Geolocation);

            loginButton.href = originalHref.UriString();
        }
    }

    function MarkerSubmission(FormData) {
        return new Promise(function (resolve, reject) {
            var oReq = new XMLHttpRequest();

            oReq.onreadystatechange = function () {
                if (oReq.readyState === 4) {
                    resolve(JSON.parse(oReq.response));
                }
            };

            oReq.open("POST", "Api/Add/NewMarker");
            oReq.send(FormData);


        });
    }

    function SubmitNewMarker(FormSubmitEvent) {
        PageBusyOverlay.Show("Submitting...");

        FormSubmitEvent.preventDefault();
        //console.log("FormSubmitEvent:", FormSubmitEvent);
        var formData = new FormData(FormSubmitEvent.target);

        // I don't really do anything with this yet (I'll probably show a modal or something later
        MarkerSubmission(formData).then(function (newFeature) {
            PageBusyOverlay.Hide();
            DeactivateAddMarkerForm();
            console.log(newFeature);
            RenderCharities(ReturnCoordObject(newFeature.geometry.coordinates[0], newFeature.geometry.coordinates[1]));
        });
    }

    function InterceptFormSubmission() {
        // bind a function to the event once
        BindOnce(_NewMarkerForm, "submit", "SubmitEvent", SubmitNewMarker);
    }

    function DeactivateAddMarkerForm() {
        // Hide the temp marker
        if (_TempCharityMarker !== null) {
            _TempCharityMarker.remove();
        }

        _NewMarkerForm.classList.add("is-hidden");

        // If the user is logged in, we'll have a form loaded, so we'll prefill the long/lat
        if (_NewMarkerForm.querySelector("form") !== null) {
            var locationDisplay = _NewMarkerForm.querySelector("#Location-Display");
            var locationField = _NewMarkerForm.querySelector("#Charity-Location");
            var charityName = _NewMarkerForm.querySelector("#Charity-Name");
            var checkboxes = _NewMarkerForm.querySelectorAll("[name='Doing']");

            // Empty input fields
            locationDisplay.value = "";
            locationField.value = "";
            charityName.value = "";
            // Uncheck all boxes
            for (var i = 0; i < checkboxes.length; i++) {
                var box = checkboxes[i];
                box.checked = false;
            }
        }
    }


    // Radius in meters
    function GetCharitiesNearLocation(CenterPoint, Radius) {
        return new Promise(function (resolve, reject) {
            var oReq = new XMLHttpRequest();
            oReq.onreadystatechange = function () {
                if (oReq.readyState === 4) {
                    resolve(JSON.parse(oReq.response));
                }
            };
            oReq.open("GET", "Api/GetMarkers?Longitude=" + CenterPoint.Longitude + "&Latitude=" + CenterPoint.Latitude + "&Radius=" + Radius);
            oReq.send();
        });
    }

    // Radius in meters
    function LoadCharityDetailsFromLocation(CenterPoint, Radius) {
        return new Promise(function (resolve, reject) {
            var oReq = new XMLHttpRequest();
            oReq.onreadystatechange = function () {
                if (oReq.readyState === 4) {
                    resolve(JSON.parse(oReq.response));
                }
            };
            oReq.open("GET", "Api/GetMarkerDetails?Longitude=" + CenterPoint.Longitude + "&Latitude=" + CenterPoint.Latitude + "&Radius=" + Radius);
            oReq.send();
        });
    }

    function RenderDetailList(CharitiesFound) {
        console.log(CharitiesFound);

        var markerDetails = document.querySelector("#marker-details");
        markerDetails.innerHTML = null;

        // Can't wait to just do this all with Vue later on.
        // Until then why don't I just do it manually and punch myself in the dick instead.
        for (var i in CharitiesFound) {

            var _charityData = CharitiesFound[i];
            var _charityDetails = _charityData.charityDetails;

            var charityParent = document.createElement("div");
            charityParent.classList.add("columns", "is-gapless", "is-marginless", "charity-entry");

            // Charity Icon
            var charityIcon = document.createElement("figure");
            charityIcon.classList.add("image", "is-64x64");
            charityIcon.style.backgroundColor = "#" + _charityDetails.style.primaryColour;
            // Charity Icon container
            var iconDiv = document.createElement("div");
            iconDiv.classList.add("column", "is-narrow", "charity-icon");
            // Append the icon to it's container
            iconDiv.appendChild(charityIcon);

            // append the icon container to the charity div
            charityParent.appendChild(iconDiv);

            // Charity details list: Name, Doing/rating, distance, etc...
            // Details Container
            var detailsDiv = document.createElement("div");
            detailsDiv.classList.add("column", "charity-details-container");
            // Charity name header
            var charityName = document.createElement("h3");
            charityName.classList.add("title", "is-5", "charity-name");
            charityName.innerText = _charityDetails.name;
            // Append the name to the details container
            detailsDiv.appendChild(charityName);

            // Doing/Rating, distance etc container
            var charityDetails = document.createElement("div");
            charityDetails.classList.add("columns", "is-gapless", "charity-details");

            /*
            // What they were tagged as
            var doingContainer = document.createElement("div");
            doingContainer.classList.add("column");
            // if we have anything tagged under doing, loop over and add them
            if (_charityData.doing.length > 0) {
                for (var i = 0; i < _charityData.doing.length; i++) {
                    var doingTag = document.createElement("span");
                    doingTag.innerHTML = _charityData.doing[i];
                    doingContainer.appendChild(doingTag);
                }
            }
            // Append it to the charity details container
            charityDetails.appendChild(doingContainer);
            */

            // How long ago the charity was seen
            var lastSeenContainer = document.createElement("div");
            lastSeenContainer.classList.add("column", "last-seen");

            var timestamp = /(\d+):(\d+):(\d+).(\d+)/g.exec(_charityData.lastSeen);

            var ts = {
                Minutes : parseInt(timestamp[2]),
                Hours : parseInt(timestamp[1])
            };

            var timeTag = document.createElement("span");
            // add an icon
            var timeIcon = document.createElement("i");
            timeIcon.classList.add("fa", "fa-clock-o");
            timeTag.appendChild(timeIcon);

            var timespan = document.createElement("span");
            timespan.classList.add("tagged-time");
            timespan.innerHTML = "Tagged ";
            if (ts.Hours > 0) {
                timespan.innerHTML += ts.Hours + (ts.Hours === 1 ? " hour": " hours ");
            }

            if (ts.Minutes > 0) {
                timespan.innerHTML += ts.Minutes + (ts.Minutes === 1 ? " minute" : " minutes") + " ago";
            } else {
                timespan.innerHTML += " just now";
            }

            timeTag.appendChild(timespan);

            lastSeenContainer.appendChild(timeTag);

            // Append it to the charity details container
            charityDetails.appendChild(lastSeenContainer);


            // Distance from selected marker container
            var distanceContainer = document.createElement("div");
            distanceContainer.classList.add("column", "marker-distance");
            var distanceInMeters = document.createElement("span");
            // The first marker will usually have a distance less than 1m, based on geohashing the location previously.
            // So there's a very small chance that one day, someone will click a marker and it's distance to itself will be 0!
            // But I don't really care about that small chance (yet), so treat any marker with a distance to itself less than 1 to be itself.
            if (_charityData.distance < 1) {
                distanceInMeters.innerHTML = "Selected marker";
            } else {
                distanceInMeters.innerHTML = "Distance from selected marker: " + _charityData.distance + "m";
            }
            distanceContainer.appendChild(distanceInMeters);
            // append the distance container to the details container
            charityDetails.appendChild(distanceContainer);
            // TODO: Render how long ago they were seen here

            // append the charity details to the details container
            detailsDiv.appendChild(charityDetails);

            // Append the details to the charity div
            charityParent.appendChild(detailsDiv);

            markerDetails.appendChild(charityParent);
        }
    }

    // Super advanced function here!
    function UnrenderDetailList() {
        var markerDetails = document.querySelector("#marker-details");
        markerDetails.innerHTML = null;
    }



    var AutoCompleteCharity = Debounce(AutoCompleteCharitySuggestion, 250);

    function AutoCompleteCharitySuggestion(Event) {
        function Search(Term) {
            return new Promise(function (resolve, reject) {
                var oReq = new XMLHttpRequest();
                oReq.onreadystatechange = function () {
                    if (oReq.readyState === 4) {
                        resolve(JSON.parse(oReq.response));
                    }
                };
                oReq.open("GET", "Api/GetCharitySuggestion?name=" + Term);
                oReq.send();
            });
        }

        function AutocompleteItemEvent(CharityName, Term) {
            _NewMarkerForm.querySelector("#Charity-Name").value = CharityName;

            // Bump the selected term for autocomplete scores
            var oReq = new XMLHttpRequest();
            oReq.onreadystatechange = function () {
                if (oReq.readyState === 4) {
                    console.log("Bumped name " + CharityName + " for term " + Term);
                }
            };
            oReq.open("GET", "Api/BumpCharitySuggestion?name=" + CharityName + "&Term=" + Term);
            //oReq.withCredentials = true;
            oReq.send();


            ClearAutoComplete();
        }

        function ClearAutoComplete() {
            var autocompleteTarget = document.querySelector("#autocomplete-container");
            autocompleteTarget.innerHTML = null;
        }


        Search(Event.target.value)
            .then(function (res) {
                // this is garbage, I need to _not_ query for this selector every time this function fires.
                var autocompleteTarget = document.querySelector("#autocomplete-container");
                autocompleteTarget.innerHTML = null;
                if (res.length > 0) {
                    for (var i in res) {
                        var item = document.createElement("li");
                        item.innerHTML = res[i];
                        item.addEventListener("click", function () { AutocompleteItemEvent(res[i], Event.target.value) });
                        autocompleteTarget.appendChild(item);
                        //console.log(res[i]);
                    }
                }
            });
    }

    return {
        RenderMap: RenderMap
    };
};