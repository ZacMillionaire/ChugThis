using Microsoft.AspNetCore.Authentication.OAuth;
using Nulah.ChugThis.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nulah.ChugThis.Models.Users;

namespace Nulah.ChugThis.Controllers.Users {
    public class UserOAuth {
        internal static async Task RegisterUser(OAuthCreatingTicketContext context, Provider LoginProvider /* change this later */, IDatabase Redis, AppSettings Settings) {
            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue(LoginProvider.AuthorizationHeader, context.AccessToken);

            // Extract the user info object from the OAuth response
            var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();
            var oauthres = await response.Content.ReadAsStringAsync();
            // I hope this should be generic enough for most OAuth /me responses.
            // I'm almost sure they'll always have a name field, and I'm 100% sure they'll always have an id field.
            var identity = JsonConvert.DeserializeObject<User>(oauthres, new JsonSerializerSettings {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

            context.Identity.AddClaims(
                new List<Claim> {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        $"{LoginProvider.ProviderShort}-{identity.Id}",
                        ClaimValueTypes.String,
                        context.Options.ClaimsIssuer
                    ),
                    new Claim(
                        ClaimTypes.GivenName,
                        identity.Name,
                        ClaimValueTypes.String,
                        context.Options.ClaimsIssuer
                    ),
                    new Claim(
                        "RedisKey",
                        $"{Settings.ConnectionStrings.Redis.BaseKey}Users:{LoginProvider.ProviderShort}-{identity.Id}",
                        ClaimValueTypes.String,
                        context.Options.ClaimsIssuer
                    )
                }
            );
        }

        // If we're here it's probably because of Reddit's OAuth being broken/garbage, or ASP .Net Core 2.0 having a fucking stupid auth library.
        // Throw a dart at a board and thats your answer.
        // If you were using GitHub or Facebook, you'll probably have got a useful error of some sort in the url.
        internal static Task OAuthRemoteFailure(RemoteFailureContext context, Provider loginProvider, IDatabase redis, AppSettings applicationSettings) {
            //throw new Exception("Fuck it");
            return Task.CompletedTask;
        }

    }
}