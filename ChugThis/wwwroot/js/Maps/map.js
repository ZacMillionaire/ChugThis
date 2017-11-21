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
            FormTarget: "#add-new-marker-form"
        };
    }

    var __self = this;
    var _MapBoxApiKey = ApiKey;
    var _UserLocation = null;
    var _Options = Options;
    var _TempFuckwitMarker = null;
    var _TouchDrag = false;
    var _LoadedFuckwitMarkers = [];
    var _NewMarkerForm = document.querySelector(_Options.FormTarget);

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

            // load the initial fuckwits around the users location
            RenderFuckwits(_UserLocation, _MapBox);

            // User Location Tracking (geospatially, not analytics)
            //AddUserLocation(_MapBox);

            _MapLoaded = true;

        });

        _MapBox.on("dragend", function (e) {
            var mapcenter = _MapBox.getCenter();
            RenderFuckwits(ReturnCoordObject(mapcenter.lng, mapcenter.lat));
        });
        _MapBox.on("zoomend", function (e) {
            var mapcenter = _MapBox.getCenter();
            RenderFuckwits(ReturnCoordObject(mapcenter.lng, mapcenter.lat));
        });

        // Trigger the add marker
        _MapBox.on("click", function (e) {
            AddFuckwitMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent);
        });

        _MapBox.on("touchstart", function (e) {
            _TouchDrag = false;
        });
        // Trigger the add marker for mobile
        _MapBox.on("touchend", function (e) {
            if (_TouchDrag === false) {
                AddFuckwitMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent);
            }
        });

        // disable tap events if a drag occurs (user is probably panning or pinch zooming, not tapping)
        _MapBox.on("touchmove", function (e) {
            _TouchDrag = true;
        });

    }

    function RenderFuckwits(GeoLoc) {

        console.log("Rendering fuckwits around", GeoLoc);

        // remove all previously loaded markers
        _LoadedFuckwitMarkers.forEach(function (marker) {
            marker.remove();
        });


        function MoveToFuckwitMarker(Event, Marker) {
            Event.preventDefault();
            var infobox = document.getElementById("info");
            infobox.innerHTML = JSON.stringify(Marker);
            try {
                _MapBox.flyTo({ center: Marker.geometry.coordinates, zoom: _Options.Zoom.Desktop });
            } catch (ex) {
                infobox.innerHTML = ex;
            }
            Event.stopPropagation(); // prevent any other things from firing
        }

        _LoadedFuckwitMarkers = [];
        GetFuckwitsNearLocation(GeoLoc).features.forEach(function (marker) {
            // create a HTML element for each feature
            var el = document.createElement('div');
            el.className = 'custom-marker-CHANGEME';
            el.style.backgroundColor = marker.properties["fuckwit-color"];

            // make a marker for each feature and add to the map
            var m = new mapboxgl.Marker(el)
                .setLngLat(marker.geometry.coordinates)
                .addTo(_MapBox);

            el.addEventListener("click", function (e) {
                MoveToFuckwitMarker(e, marker);
            });

            el.addEventListener("touchstart", function (e) {
                _TouchDrag = false;
            });
            // Trigger the add marker for mobile
            el.addEventListener("touchend", function (e) {
                if (_TouchDrag === false) {
                    MoveToFuckwitMarker(e, marker);
                }
            });

            // disable tap events if a drag occurs (user is probably panning or pinch zooming, not tapping)
            el.addEventListener("touchmove", function (e) {
                _TouchDrag = true;
            });


            _LoadedFuckwitMarkers.Add(m);
        });
    }

    function AddFuckwitMarker(GeoObject, OriginalEvent) {

        // Do nothing if the event came from anything other than the map canvas (eg the user clicked on the marker they already placed)
        if (OriginalEvent.target.tagName !== "CANVAS") {
            return;
        }

        // remove the previously added fuckwit
        if (_TempFuckwitMarker !== null) {
            _TempFuckwitMarker.remove();
        }

        var el = document.createElement('div');
        el.className = 'add-marker-CHANGEME';
        el.innerText = "fuckwit";
        el.style.backgroundColor = "#006633";

        _TempFuckwitMarker = new mapboxgl.Marker(el)
            .setLngLat([GeoObject.Longitude, GeoObject.Latitude])
            .addTo(_MapBox);

        console.log("adding fuckwit", GeoObject);
        _MapBox.flyTo({ center: [GeoObject.Longitude, GeoObject.Latitude], zoom: _Options.Zoom.Desktop });

        ActivateAddMarkerForm(GeoObject);
    }

    function ActivateAddMarkerForm(Geolocation) {
        var locationDisplay = _NewMarkerForm.querySelector("#Location-Display");
        var locationField = _NewMarkerForm.querySelector("#Charity-Location");
        locationDisplay.value = Geolocation.Longitude + " " + Geolocation.Latitude;
        locationField.value = Geolocation.Longitude + " " + Geolocation.Latitude;
    }

    // TODO: Make this an API call
    function GetFuckwitsNearLocation(CenterPoint) {
        return {
            "type": "FeatureCollection",
            "features": [
                {
                    "type": "Feature",
                    "properties": {
                        "fuckwit-color": "#f0f",
                        "fuckwit-id": 1
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
                        "fuckwit-color": "#ff0",
                        "fuckwit-id": 2
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
                        "fuckwit-color": "#3f5",
                        "fuckwit-id": 3
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
                        "fuckwit-color": "#00f",
                        "fuckwit-id": Math.floor(((Math.random() * 10)))
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
        };
    }

    return {
        RenderMap: RenderMap
    };
};