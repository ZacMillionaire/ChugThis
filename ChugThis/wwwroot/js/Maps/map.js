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
            RenderCharities(_UserLocation, _MapBox);

            // User Location Tracking (geospatially, not analytics)
            //AddUserLocation(_MapBox);

            _MapLoaded = true;

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
            AddCharityMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent);
        });

        _MapBox.on("touchstart", function (e) {
            _TouchDrag = false;
        });
        // Trigger the add marker for mobile
        _MapBox.on("touchend", function (e) {
            if (_TouchDrag === false) {
                AddCharityMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent);
            }
        });

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
            //var infobox = document.getElementById("info");
            //infobox.innerHTML = JSON.stringify(Marker);
            try {
                _MapBox.flyTo({ center: Marker.geometry.coordinates, zoom: _Options.Zoom.Desktop });
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
                el.className = 'custom-marker-CHANGEME';
                el.style.backgroundColor = "#" + marker.properties["Marker-Colour"];
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

    function AddCharityMarker(GeoObject, OriginalEvent) {

        // Do nothing if the event came from anything other than the map canvas (eg the user clicked on the marker they already placed)
        if (OriginalEvent.target.tagName !== "CANVAS") {
            return;
        }

        // remove the previously added fuckwit
        if (_TempCharityMarker !== null) {
            _TempCharityMarker.remove();
        }

        var el = document.createElement('div');
        el.className = 'add-marker-CHANGEME';
        el.innerText = "Charity";
        el.style.backgroundColor = "#006633";

        _TempCharityMarker = new mapboxgl.Marker(el)
            .setLngLat([GeoObject.Longitude, GeoObject.Latitude])
            .addTo(_MapBox);

        console.log("adding Charity", GeoObject);
        _MapBox.flyTo({ center: [GeoObject.Longitude, GeoObject.Latitude], zoom: _Options.Zoom.Desktop });

        ActivateAddMarkerForm(GeoObject);
    }

    function ActivateAddMarkerForm(Geolocation) {
        _NewMarkerForm.classList.remove("is-hidden");
        var locationDisplay = _NewMarkerForm.querySelector("#Location-Display");
        var locationField = _NewMarkerForm.querySelector("#Charity-Location");
        locationDisplay.value = Geolocation.Longitude + " " + Geolocation.Latitude;
        locationField.value = Geolocation.Longitude + " " + Geolocation.Latitude;
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