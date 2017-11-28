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
    //    var _MapBox = null;

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
        if (_MapBoxApiKey === null) {
            // TODO: change this to a modal thing later
            alert("Failed to render map. See console for details.");
            console.error("No MapBox Api key given");
            return;
        }

        _UserLocation = ReturnCoordObject(StartingPosition.coords.longitude, StartingPosition.coords.latitude);

        mapboxgl.accessToken = _MapBoxApiKey;

        _MapBox = new mapboxgl.Map({
            container: _Options.Container, // container id
            style: 'mapbox://styles/mapbox/streets-v9',
            center: [_UserLocation.Longitude, _UserLocation.Latitude], // starting position, [long,lat]
            zoom: _Options.Zoom.StartZoom // starting zoom
        });



        _MapBox.addControl(new mapboxgl.GeolocateControl({
            positionOptions: {
                enableHighAccuracy: true,
                maximumAge: 1000
            },
            trackUserLocation: true
        }));


        _MapBox.on("load", function () {
            console.log("loaded");

            console.log(_MapBox);

            // load the initial fuckwits around the users location
            RenderCharities(_UserLocation);

            // User Location Tracking (geospatially, not analytics)
            //AddUserLocation(_MapBox);

            _MapLoaded = true;

            try {

                // This is kind of horrible, but it's done so if a user isn't logged in when they tap to add a marker,
                // we redirect them to the form with where they tapped so they can add the marker they had planned with minimal fuss.
                var match = RegExp('[?&]lnglat=([^&]*)').exec(window.location.search);
                if (match) {
                    var lnglat = decodeURIComponent(match[1].replace(/\+/g, ' ')).split(',');
                    console.log(window.location.search);
                    var queryStringGeo = ReturnCoordObject(lnglat[0], lnglat[1]);
                    AddCharityMarker(queryStringGeo, "PageLoad", _Options.Zoom.StartZoom);
                }
            } catch (e) {
                console.error(e);
            }

        });

        _MapBox.on("dragend", function (e) {
            var mapcenter = _MapBox.getCenter();
            RenderCharities(ReturnCoordObject(mapcenter.lng, mapcenter.lat));
        });
        _MapBox.on("zoomend", function (e) {
            var mapcenter = _MapBox.getCenter();
            RenderCharities(ReturnCoordObject(mapcenter.lng, mapcenter.lat));
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

        var bbox = _MapBox.getContainer().getBoundingClientRect();
        var width = bbox.width;
        var height = bbox.height;

        var topLeft = _MapBox.unproject([0, 0]);
        var bottomRight = _MapBox.unproject([width, height]);
        /*
        debug markers
        var el1 = document.createElement('div');
        el1.className = 'add-marker-CHANGEME';
        el1.innerText = "tl";
        el1.style.backgroundColor = "#f00";

        new mapboxgl.Marker(el1)
            .setLngLat(topLeft)
            .addTo(_MapBox);

        var el2 = document.createElement('div');
        el2.className = 'add-marker-CHANGEME';
        el2.innerText = "br";
        el2.style.backgroundColor = "#0f0";

        new mapboxgl.Marker(el2)
            .setLngLat(bottomRight)
            .addTo(_MapBox);*/

        var diagonalDistance = calcCrow(topLeft.lat, topLeft.lng, bottomRight.lat, bottomRight.lng);
        //console.log("diagonal dist", diagonalDistance + "m");

        // Shamelessly stolen from SnackOverflow until I have time to rewrite it
        // https://stackoverflow.com/questions/18883601/function-to-calculate-distance-between-two-coordinates-shows-wrong
        //This function takes in latitude and longitude of two location and returns the distance between them as the crow flies (in km * 1000 (meters))
        function calcCrow(lat1, lon1, lat2, lon2) {
            var R = 6371; // km
            var dLat = toRad(lat2 - lat1);
            var dLon = toRad(lon2 - lon1);
            var lat1 = toRad(lat1);
            var lat2 = toRad(lat2);

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


        console.log("Rendering Charities around", GeoLoc, "with radius", (diagonalDistance / 4) + "m");


        function MoveToCharityMarker(Event, Marker) {
            Event.preventDefault();

            DeactivateAddMarkerForm();

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

            console.log("Promise complete", GeoJsonResult);

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
        });

    }

    function AddCharityMarker(GeoObject, OriginalEvent, Zoom) {

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

        _TempCharityMarker = new mapboxgl.Marker(el)
            .setLngLat([GeoObject.Longitude, GeoObject.Latitude])
            .addTo(_MapBox);

        console.log("adding Charity", GeoObject);
        _MapBox.flyTo({ center: [GeoObject.Longitude, GeoObject.Latitude], zoom: Zoom });

        ActivateAddMarkerForm(GeoObject);
    }

    function ActivateAddMarkerForm(Geolocation) {
        // Show the new marker form
        _NewMarkerForm.classList.remove("is-hidden");
        // If the user is logged in, we'll have a form loaded, so we'll prefill the long/lat
        if (_NewMarkerForm.querySelector("form") !== null) {
            var locationDisplay = _NewMarkerForm.querySelector("#Location-Display");
            var locationField = _NewMarkerForm.querySelector("#Charity-Location");
            locationDisplay.value = Geolocation.Longitude + " " + Geolocation.Latitude;
            locationField.value = Geolocation.Longitude + " " + Geolocation.Latitude;
        } else {
            var loginButton = _NewMarkerForm.querySelector("#new-marker-login");
            var lnglatparam = "/?lnglat=" + Geolocation.Longitude + "," + Geolocation.Latitude;
            var baseuri = loginButton.href;
            // console.log(baseuri + "?RedirectUri=" + encodeURI(lnglatparam));
            loginButton.href = baseuri + "?RedirectUri=" + encodeURI(lnglatparam);
        }
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


    // TODO: Make this an API call
    function GetCharitiesNearLocation(CenterPoint, Radius) {
        return new Promise(function (resolve, reject) {
            var oReq = new XMLHttpRequest();

            oReq.onreadystatechange = function () {
                if (oReq.readyState === 4) {
                    //console.log("---");
                    //console.log("Request done", JSON.parse(oReq.response));
                    //console.log("---");

                    resolve(JSON.parse(oReq.response));
                }
            }

            oReq.open("GET", "Api/GetMarkers?Longitude=" + CenterPoint.Longitude + "&Latitude=" + CenterPoint.Latitude + "&Radius=" + Radius);
            oReq.send();


        });

        /*
        return {
            "type": "FeatureCollection",
            "features": [
                {
                    "type": "Feature",
                    "properties": {
                        "Marker-Colour": "#f0f",
                        "Marker-Id": 1
                    },
                    "geometry": {
                        "type": "Point",
                        "coordinates": [
                            153.00760368874035,
                            -27.4677551075602,
                        ]
                    }
                },
                {
                    "type": "Feature",
                    "properties": {
                        "Marker-Colour": "#ff0",
                        "Marker-Id": 2
                    },
                    "geometry": {
                        "type": "Point",
                        "coordinates": [
                            153.00777106795368,
                            -27.4677551075602,
                        ]
                    }
                },
                {
                    "type": "Feature",
                    "properties": {
                        "Marker-Colour": "#3f5",
                        "Marker-Id": 3
                    },
                    "geometry": {
                        "type": "Point",
                        "coordinates": [
                            153.00760368874035,
                            -27.46780222416966,
                        ]
                    }
                },
                {
                    "type": "Feature",
                    "properties": {
                        "Marker-Colour": "#00f",
                        "Marker-Id": Math.floor(((Math.random() * 10)))
                    },
                    "geometry": {
                        "type": "Point",
                        "coordinates": [
                            153.00989439999998,
                            -27.4657359,
                        ]
                    }
                }
            ]
        };*/
    }

    return {
        RenderMap: RenderMap
    };
};