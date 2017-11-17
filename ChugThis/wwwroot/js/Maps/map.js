if ("geolocation" in navigator) {


    var _MapBox,
        _UserLocation,
        _MapLoaded = false;

    function ReturnCoordObject(Long, Lat) {
        return {
            Latitude: Lat,
            Longitude: Long
        };
    }

    // CenterPoint is from ReturnCoordObject
    function GetFuckwitsNearLocation(CenterPoint) {

        console.log(CenterPoint);

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

    function RenderMapBox(initialPosition) {
        console.log("Loading...")

        console.log(initialPosition.coords);
        _UserLocation = ReturnCoordObject(initialPosition.coords.longitude, initialPosition.coords.latitude);

        mapboxgl.accessToken = "@ViewData["MapBoxKey"]";
        _MapBox = new mapboxgl.Map({
            container: 'mapbox-container', // container id
            style: 'mapbox://styles/mapbox/streets-v9',
            center: [_UserLocation.Longitude, _UserLocation.Latitude], // starting position, [long,lat]
            zoom: 16 // starting zoom
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
            AddUserLocation(_MapBox);

            _MapLoaded = true;

        });

        _MapBox.on("dragend", function (e) {
            var mapcenter = _MapBox.getCenter();
            console.log(mapcenter);
            RenderFuckwits(ReturnCoordObject(mapcenter.lng, mapcenter.lat), _MapBox);
            /*
            _MapBox.getSource("fuckwits")
                .setData(GetFuckwitsNearLocation(ReturnCoordObject(mapcenter.lng, mapcenter.lat)));*/
        });

        // Trigger the add marker
        _MapBox.on("click", function (e) {
            AddFuckwitMarker(ReturnCoordObject(e.lngLat.lng, e.lngLat.lat));
        });
    }

    function RenderFuckwits(GeoLoc, MapBox) {
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
                alert("custom click");
                _MapBox.flyTo({ center: marker.geometry.coordinates, zoom: 19 });
                e.stopPropagation(); // prevent any other things from firing
            });
        });

    }

    // MapPoint: [x,y]
    // returns true if there are no symbols or cluster markers at the clicked MapPoint.
    // If false, it means the user is clicking a symbol, and not clicking to add a new fuckwit location
    function ClickLocationHasNoLayerFeatures(MapPoint) {
        var features = _MapBox.queryRenderedFeatures(MapPoint, { layers: ["clusters", "symbols"] });
        if (features.length === 0) {
            return true;
        } else {
            return false;
        }
    }

    var tempFuckwit = null, markerTooltip = null;

    // Starts the marker adding process
    // will shrink the map down and open the form control or something.
    // would like to do it with as minimal javascript as possible.
    function AddFuckwitMarker(GeoObject) {

        // remove the previously added fuckwit
        if (tempFuckwit !== null) {
            tempFuckwit.remove();
        }

        var el = document.createElement('div');
        el.className = 'custom-marker-CHANGEME';
        el.innerText = "fuckwit";
        el.style.backgroundColor = "#006633";

        tempFuckwit = new mapboxgl.Marker(el)
            .setLngLat([GeoObject.Longitude, GeoObject.Latitude])
            .addTo(_MapBox);

        console.log(GeoObject);
        //document.getElementById("mapbox-container").classList.add("small-view");
        //setTimeout(function () {
        //_MapBox.resize();
        console.log("adding fuckwit", GeoObject);
        _MapBox.flyTo({ center: [GeoObject.Longitude, GeoObject.Latitude]/*, zoom: 19*/ });
        //}, 250);
    }

    function BindMouseEvents(LayerTarget, ZoomOptions, ClickFunction) {

        // auto zoom for layers
        _MapBox.on('click', LayerTarget, function (e) {
            console.log(LayerTarget + " click", e);
            if (ClickFunction !== undefined) {
                ClickFunction(e);
            }
            _MapBox.flyTo({ center: e.features[0].geometry.coordinates, zoom: ZoomOptions.DesktopZoom });
        });

        // for mobile
        _MapBox.on('touchstart', LayerTarget, function (e) {
            _MapBox.flyTo({ center: e.features[0].geometry.coordinates, zoom: ZoomOptions.MobileZoom });
        });

        // Change the cursor to a pointer when the it enters a feature in the 'symbols' layer.
        _MapBox.on('mouseenter', LayerTarget, function () {
            _MapBox.getCanvas().style.cursor = 'pointer';
        });

        // Change it back to a pointer when it leaves.
        _MapBox.on('mouseleave', LayerTarget, function () {
            _MapBox.getCanvas().style.cursor = '';
        });
    }

    function ShowMarkerTooltip(e) {
        console.log("TOOLTIP", e);
        /*
        // remove the previously added fuckwit
        if (tempFuckwit !== null) {
            tempFuckwit.remove();
        }

        var el = document.createElement('div');
        el.className = 'custom-marker-CHANGEME';
        el.innerText = "fuckwit";
        tempFuckwit = new mapboxgl.Marker(el)
            .setLngLat([GeoObject.Longitude, GeoObject.Latitude])
            .addTo(_MapBox);
*/
    }

    function debounce(func, wait, immediate) {
        var timeout;
        return function () {
            var context = this, args = arguments;
            var later = function () {
                timeout = null;
                if (!immediate) func.apply(context, args);
            };
            var callNow = immediate && !timeout;
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
            if (callNow) func.apply(context, args);
        };
    };
    /*
    // commented out for now, deciding to use the Mapbox default user tracker instead.
    navigator.geolocation.watchPosition(
        function (NewLocation) {
            if (_MapLoaded) {
                var previousLocation = _UserLocation;
                _UserLocation = ReturnCoordObject(NewLocation.coords.latitude, NewLocation.coords.longitude);
                // Weird double call prevention, it's probably a Chome devtools thing, but who knows with JavaScript
                if (JSON.stringify(previousLocation) !== JSON.stringify(_UserLocation)) {
                    UpdateUserLocation(_MapBox, previousLocation, _UserLocation);
                }
            }
        }
    );
    */
    // conflicts with watchLocation if called before it. Basically this will be ignored if you use navigator.geolocation.getCurrentPosition before watchPosition.
    // Why? Who knows. Either way this needs to be last to make sure dragpan works for map box on mobile.
    navigator.geolocation.getCurrentPosition(function (position) {
        RenderMapBox(position);
    }, function (err) {
        console.log(err); // I'd love to shove this into an XHR call to log the error later for mobile stuff.
        // probably have it trigger a modal to let the user know as well.
    });


    function UpdateUserLocation(Map, OriginalLocation, NewLocation) {
        //console.log("old", OriginalLocation, "new", NewLocation);
        Map.getSource("UserLocation")
            .setData(CurrentUserLocation());
    }

    // Looks useless, but the same code would be used in multiple places and something tells me I'll want to do more with this later.
    function CurrentUserLocation() {
        return {
            type: "Point",
            coordinates: [_UserLocation.Longitude, _UserLocation.Latitude]
        };
    }

    function AddUserLocation(Map) {

        Map.addSource('UserLocation', {
            "type": "geojson",
            "data": CurrentUserLocation()
        });

        Map.addLayer({
            id: "user",
            type: "circle",
            source: "UserLocation",
            paint: {
                "circle-radius": 10,
                "circle-color": "#f0f"
            }
        });
    }

    function UpdateLocDebug(point, lngLat, zoom, userloc) {
        document.getElementById('info').innerHTML =
            // e.point is the x, y coordinates of the mousemove event relative
            // to the top-left corner of the map
            JSON.stringify(point) + '<br />' +
            // e.lngLat is the longitude, latitude geographical position of the event
            JSON.stringify(lngLat) + "<br />" + JSON.stringify(_UserLocation);
    }
} else {
    alert("can't locate");
    /* geolocation IS NOT available */
}