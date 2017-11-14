# Temporary ReadMe
To start this successfully, you'll need to create a hosting.json file with:

```
{
  "urls": "http://*:####"
}
```

Where the `####` is the port number you wish to run this under.

You'll also need an appsettings.json file (and appsettings.Development.json if you want seperate files):

```
{
  "Config": {
    "Environment": "Release"
  },
  "ConnectionStrings": {
    "Redis": {
      "EndPoint": "{redis server ip (can be 127.0.0.1}:{redis port, default is 6379 iirc}",
      "ClientName": "{whatever you want to call the connection}",
      "AdminMode": false, // set to true if you want to do destructive commands such as FLUSHALL and non-destructive such as KEYS
      "Password": "{leave blank if no password}",
      "BaseKey": "{base key to group in redis, MUST end with a colon, a SystemException will be thrown at start up if it is missing}",
      "Database": {a number from 0-15 to indicate which database to use. Using Redis in a cluster? Set this to 0.}
    }
  },
  "Logging": {
    "Level": "Information"
  },
  "ApiKeys": {
    "MapBox": "{MapBox access token}"
  },
  "OAuthProviders": [
    {
      "ClientId": "###",
      "ClientSecret": "###",
      "Scope": [ "scopes" ],
      "SaveTokens": true,
      "AuthorizationHeader": "Bearer/token",
      "AuthenticationScheme": "Name for cookies",
      "ProviderShort": "short few letters for cookie/redis keys",
      "AuthorizationEndpoint": "oauth end point",
      "TokenEndpoint": "access token end point",
      "UserInformationEndpoint": "oauth /me end point",
      "CallbackPath": "/signin-{provider name}"
    }
  ]
}

```