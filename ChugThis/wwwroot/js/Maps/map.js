var ChugMap = function (ApiKey, Options) {

    if (Options === undefined) {
        console.warn("No options given, using defaults");
        _Options = {
            Container: 'mapbox-container',
            Zoom: {
                StartZoom: 16,
                Desktop: 19,
                Mobile: 18
            }
        };
    }

    var __self = this;
    var _MapBoxApiKey = ApiKey;
    var _UserLocation = null;
    var _Options = Options;
    var _TempFuckwitMarker = null;

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
            RenderFuckwits(ReturnCoordObject(mapcenter.lng, mapcenter.lat), _MapBox);
        });
        _MapBox.on("zoomend", function (e) {
            var mapcenter = _MapBox.getCenter();
            RenderFuckwits(ReturnCoordObject(mapcenter.lng, mapcenter.lat), _MapBox);
        });

        // Trigger the add marker
        _MapBox.on("click", function (e) {
            AddFuckwitMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent);
        });

        // Trigger the add marker for mobile
        _MapBox.on("touchend", function (e) {
            AddFuckwitMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat), e.originalEvent);
        });
    }

    function RenderFuckwits(GeoLoc, MapBox) {

        console.log("Rendering fuckwits around", GeoLoc);

        function MoveToFuckwitMarker(Event, Marker) {
            _MapBox.flyTo({ center: Marker.geometry.coordinates, zoom: _Options.Zoom.Desktop });
            Event.stopPropagation(); // prevent any other things from firing
        }

        GetFuckwitsNearLocation(GeoLoc).features.forEach(function (marker) {
            // create a HTML element for each feature
            var el = document.createElement('div');
            el.className = 'custom-marker-CHANGEME';
            el.style.backgroundColor = marker.properties["fuckwit-color"];

            // make a marker for each feature and add to the map
            new mapboxgl.Marker(el)
                .setLngLat(marker.geometry.coordinates)
                .addTo(MapBox);

            el.addEventListener("click", function (e) {
                MoveToFuckwitMarker(e, marker);
            });
            el.addEventListener("touchend", function (e) {
                MoveToFuckwitMarker(e, marker);
            })
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
    }

    // TODO: Make this an API call
    function GetFuckwitsNearLocation(CenterPoint) {
        return {
            "type": "FeatureCollection",
            "features": [
                {
                    "type": "Feature",
                    "properties": {
                        "fuckwit-color": "#f0f"
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
                        "fuckwit-color": "#ff0"
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
                        "fuckwit-color": "#3f5"
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
                        "fuckwit-color": "#00f"
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