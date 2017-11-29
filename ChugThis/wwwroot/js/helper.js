var GeoHash = {
    // Adapted from: https://github.com/davetroy/geohash-js/blob/master/geohash.js
    // may/may not use it
    Encode: function encodeGeoHash(Longitude, Latitude) {
        var is_even = 1;
        var i = 0;
        var lat = []; var lon = [];
        var bit = 0;
        var ch = 0;
        var precision = 12;
        geohash = "";
        BITS = [16, 8, 4, 2, 1];
        BASE32 = "0123456789bcdefghjkmnpqrstuvwxyz";

        lat[0] = -90.0; lat[1] = 90.0;
        lon[0] = -180.0; lon[1] = 180.0;

        while (geohash.length < precision) {
            if (is_even) {
                mid = (lon[0] + lon[1]) / 2;
                if (Longitude > mid) {
                    ch |= BITS[bit];
                    lon[0] = mid;
                } else
                    lon[1] = mid;
            } else {
                mid = (lat[0] + lat[1]) / 2;
                if (Latitude > mid) {
                    ch |= BITS[bit];
                    lat[0] = mid;
                } else
                    lat[1] = mid;
            }

            is_even = !is_even;
            if (bit < 4)
                bit++;
            else {
                geohash += BASE32[ch];
                bit = 0;
                ch = 0;
            }
        }
        return geohash;
    },
    Decode: function decodeGeoHash(geohash) {
        var is_even = 1;
        var lat = []; var lon = [];
        lat[0] = -90.0; lat[1] = 90.0;
        lon[0] = -180.0; lon[1] = 180.0;
        lat_err = 90.0; lon_err = 180.0;
        BITS = [16, 8, 4, 2, 1];
        BASE32 = "0123456789bcdefghjkmnpqrstuvwxyz";

        for (i = 0; i < geohash.length; i++) {
            c = geohash[i];
            cd = BASE32.indexOf(c);
            for (j = 0; j < 5; j++) {
                mask = BITS[j];
                if (is_even) {
                    lon_err /= 2;
                    this.RefineInterval(lon, cd, mask);
                } else {
                    lat_err /= 2;
                    this.RefineInterval(lat, cd, mask);
                }
                is_even = !is_even;
            }
        }
        lat[2] = (lat[0] + lat[1]) / 2;
        lon[2] = (lon[0] + lon[1]) / 2;

        return { Latitude: lat, Longitude: lon };
    },
    RefineInterval: function refine_interval(interval, cd, mask) {
        if (cd & mask) {
            interval[0] = (interval[0] + interval[1]) / 2;
        }
        else {
            interval[1] = (interval[0] + interval[1]) / 2;
        }
    }
};

var PageBusyOverlay = {
    Show: function _showOverlay(LoadingText) {
        var htmlRoot = document.querySelector("html");
        var pageBusy = document.querySelector("#page-busy-overlay");
        pageBusy.querySelector("#loading-text").innerHTML = LoadingText;
        if (!htmlRoot.classList.contains("page-busy")) {
            htmlRoot.classList.add("page-busy");
            if (pageBusy.classList.contains("error-overlay")) {
                pageBusy.classList.remove("error-overlay");
            }

            if (!pageBusy.classList.contains("show")) {
                pageBusy.classList.add("show");
            }
        }

    },
    Hide: function _hideOverlay() {
        var htmlRoot = document.querySelector("html");
        var pageBusy = document.querySelector("#page-busy-overlay");
        pageBusy.querySelector("#loading-text").innerHTML = "";
        if (htmlRoot.classList.contains("page-busy")) {
            htmlRoot.classList.remove("page-busy");
            if (pageBusy.classList.contains("show")) {
                pageBusy.classList.remove("show");
            }
            if (pageBusy.classList.contains("error-overlay")) {
                pageBusy.classList.remove("error-overlay");
            }
        }
    },
    Error: function _errorOverlay(ErrorMessage) {
        // Hides the page overlay, if any
        this.Hide();

        var htmlRoot = document.querySelector("html");
        var pageError = document.querySelector("#page-error-overlay");
        pageError.querySelector("#error-text").innerHTML = ErrorMessage;
        if (!htmlRoot.classList.contains("page-error")) {
            htmlRoot.classList.add("page-error");
            if (!pageError.classList.contains("show")) {
                pageError.classList.add("show");
            }
        }
    }
};

// Is this overkill? probably. But I fucking hate having to fuck around with query params without
// something to do it for me.
var UriHelper = {
    Exception: function (Type, Message) {
        return {
            Type: Type,
            Message: Message
        };
    },
    GetQueryParams: function (Uri) {
        try {
            var params = {
                Count: 0,
                BaseUri: "",
                UriString: function () {
                    var s = [];
                    for (var i in this) {
                        if (this[i] !== undefined) {
                            s.push(i + "=" + encodeURIComponent(this[i]));
                        }
                    }
                    return this.BaseUri + "?" + s.join("&");
                },
                UriStringRaw: function () {
                    var s = [];
                    for (var i in this) {
                        if (this[i] !== undefined) {
                            s.push(i + "=" + this[i]);
                        }
                    }
                    return this.BaseUri + "?" + s.join("&");
                },
                Add: function (Key, Value) {
                    this[Key] = Value;
                    this.Count += 1;
                    return this;
                },
                Remove: function (Key) {
                    delete this[Key];
                    this.Count -= 1;
                    return this;
                }
            };

            Object.defineProperties(params,
                {
                    "Count": {
                        enumerable: false
                    },
                    "UriString": {
                        enumerable: false
                    },
                    "UriStringRaw": {
                        enumerable: false
                    },
                    "BaseUri": {
                        enumerable: false
                    },
                    "Add": {
                        enumerable: false
                    },
                    "Remove": {
                        enumerable: false
                    }
                });

            if (Uri === undefined || Uri === null) {
                return params;
            }

            var UriSplit = Uri.split('?');

            params.BaseUri = UriSplit[0];

            if (UriSplit.length > 1) {
                var sp = UriSplit[1].split('&');
                for (var i in sp) {
                    var match = RegExp(/([\w\d]+)=([^&]*)/g).exec(sp[i]);
                    if (match) {
                        if (params[match[1]] === undefined) {
                            params[match[1]] = match[2];
                            params.Count += 1;
                        } else {
                            throw new this.Exception("NotSupportedException", "Non-Unique query parameters (non-unique param: '" + match[1] + "') are currently not supported.");
                        }
                    }
                }
            }
            return params;
        } catch (Exception) {
            console.error(Exception);
            console.error("Error parsing Uri query string:", Exception.Name + ":", Exception.Message);
        }
    }
};