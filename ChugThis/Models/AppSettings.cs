using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nulah.ChugThis.Models {

    public class AppSettings {
        public Config Config { get; set; }
        public ConnectionStrings ConnectionStrings { get; set; }
        public Logging Logging { get; set; }
        public Provider[] OAuthProviders { get; set; }
        public ApiKeys ApiKeys { get; set; }
        public Version Version { get; set; }
    }

    public class Version {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public string Environment { get; set; }
        public int Build { get; set; }
        public string BuildTime { get; set; }
        public string VersionString { get; set; }
    }

    public class Config {
        public string Environment { get; set; }
    }

    public class ConnectionStrings {
        public Redis Redis { get; set; }
    }

    public class Redis {
        public string EndPoint { get; set; }
        public string ClientName { get; set; }
        public bool AdminMode { get; set; }
        public string Password { get; set; }
        public string BaseKey { get; set; }
        public int Database { get; set; }
    }

    public class Logging {
        public LogLevel Level { get; set; }
    }

    public class Provider {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string[] Scope { get; set; }
        public bool SaveTokens { get; set; }
        public string AuthorizationHeader { get; set; }
        public string AuthenticationScheme { get; set; }
        public string AuthorizationEndpoint { get; set; }
        public string TokenEndpoint { get; set; }
        public string UserInformationEndpoint { get; set; }
        public string CallbackPath { get; set; }
        public string ProviderShort { get; set; }
    }

    public class ApiKeys {
        public string MapBox { get; set; }
    }

}