﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.OAuth;
using Nulah.ChugThis.Models;
using StackExchange.Redis;
using Nulah.ChugThis.Extensions.Redis;
using Nulah.ChugThis.Controllers.Users;
using Nulah.ChugThis.Filters;
using Nulah.ChugThis.Extensions;
using Microsoft.Extensions.Logging;
using NulahCore.Extensions.Logging;
using Nulah.ChugThis.Controllers.Maps;
using System.Runtime.InteropServices;
using System.Reflection;
using Newtonsoft.Json;
using System.Web;

namespace Nulah.ChugThis {
    public class Startup {
        private IConfiguration Configuration { get; }
        private AppSettings _ApplicationSettings { get; set; }


        public Startup(IConfiguration configuration) {
            Configuration = configuration;
            _ApplicationSettings = new AppSettings();
            Configuration.Bind(_ApplicationSettings);

            var versionFile = System.IO.Directory.GetCurrentDirectory() + "/version.json";
            // Check if a version file exists. If not, create a new one.
            if(!System.IO.File.Exists(versionFile)) {
                // If we're in development, assume this is a visual studio build
                if(_ApplicationSettings.Config.Environment == "Development") {
                    var file = System.IO.File.Create(versionFile);
                    var versionContents = new Models.Version {
                        Major = 0,
                        Minor = 0,
                        Patch = 0,
                        Build = 1,
                        BuildTime = DateTime.UtcNow.ToString("dd-MM-yyyy"),
                        Environment = _ApplicationSettings.Config.Environment,
                        VersionString = $"0.0.0-{_ApplicationSettings.Config.Environment}.1+{DateTime.UtcNow.ToString("dd-MM-yyyy")}"
                    };

                    var json = JsonConvert.SerializeObject(versionContents);
                    using(System.IO.StreamWriter sw = new System.IO.StreamWriter(file)) {
                        sw.WriteLine(json);
                    }
                } else {
                    // If we're in any other environment, throw an exception.
                    throw new SystemException("Version file missing");
                }
            }

            _ApplicationSettings.Version = JsonConvert.DeserializeObject<Models.Version>(System.IO.File.ReadAllText(versionFile));


            if(!_ApplicationSettings.ConnectionStrings.Redis.BaseKey.EndsWith(':')) {
                throw new SystemException("Redis base key must end with a colon(':')");
            }
        }

        public class Provider {
            public string ClientId { get; set; }
            public string ClientSecret { get; set; }
            public string[] Scope { get; set; }
            public bool SaveTokens { get; set; }
            /// <summary>
            ///     <para>
            /// Used for OAuth Authorization header, GitHub uses token [...], Discord uses Bearer [...]
            ///     </para><para>
            /// This is case sensitive as well. eg token != Token
            ///     </para>
            /// </summary>
            public string AuthorizationHeader { get; set; }
            public string AuthenticationScheme { get; set; }
            public string AuthorizationEndpoint { get; set; }
            public string TokenEndpoint { get; set; }
            public string UserInformationEndpoint { get; set; }
            public string CallbackPath { get; set; }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {

            IDatabase Redis = RedisStore.RedisCache;
            services.AddScoped(_ => Redis);
            services.AddScoped(_ => _ApplicationSettings);

            // Store the MapBox Api key
            MapController.SetMapBoxApiKey(Redis, _ApplicationSettings);


            var loginProviders = _ApplicationSettings.OAuthProviders;

            var OAuthService = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => options = new CookieAuthenticationOptions {
                    LoginPath = new PathString("/Login"),
                    LogoutPath = new PathString("/Logout"),
                    AccessDeniedPath = "/",
                    ExpireTimeSpan = new TimeSpan(30, 0, 0, 0),
                    SlidingExpiration = true
                });

            foreach(var loginProvider in loginProviders) {
                OAuthService.AddOAuth(loginProvider.AuthenticationScheme, options => {
                    options.ClientId = loginProvider.ClientId;
                    options.ClientSecret = loginProvider.ClientSecret;
                    options.SaveTokens = loginProvider.SaveTokens;
                    options.AuthorizationEndpoint = loginProvider.AuthorizationEndpoint;
                    options.TokenEndpoint = loginProvider.TokenEndpoint;
                    options.UserInformationEndpoint = loginProvider.UserInformationEndpoint;
                    options.CallbackPath = new PathString(loginProvider.CallbackPath);

                    foreach(var scope in loginProvider.Scope) {
                        options.Scope.Add(scope);
                    }

                    // This looks fucking ugly though, need to find out how to move it to a class
                    options.Events = new OAuthEvents {
                        // https://auth0.com/blog/authenticating-a-user-with-linkedin-in-aspnet-core/
                        // The OnCreatingTicket event is called after the user has been authenticated and the OAuth middleware has
                        // created an auth ticket. We need to manually call the UserInformationEndpoint to retrieve the user's information,
                        // parse the resulting JSON to extract the relevant information, and add the correct claims.
                        OnCreatingTicket = async context => {
                            await UserOAuth.RegisterUser(context, loginProvider, Redis, _ApplicationSettings);
                        },/*
                        // Here until I figure out what magic kestrel needs to actually work with https.
                        // Apparently it's not a thing you should do (which is why I have it proxied behind nginx): https://github.com/aspnet/KestrelHttpServer/issues/1108
                        // but it's still fucking annoying having my redirect_uri's going to http, because https causes a weird handshake bug because asdfklsflkashfdaslkf
                        // I'm a "professional", btw. There's no way you'd actually think that looking at my code though.
                        OnRedirectToAuthorizationEndpoint = context => {
                            var uri = HttpUtility.ParseQueryString(context.RedirectUri);
                            uri["redirect_uri"] = uri["redirect_uri"].Replace("http","https");
                            context.Response.Redirect(uri.ToString());
                            return Task.FromResult(0);
                        },*/
                        OnRemoteFailure = async context => {
                            await UserOAuth.OAuthRemoteFailure(context, loginProvider, Redis, _ApplicationSettings);
                            context.HttpContext.Response.StatusCode = 500;
                        }
                    };
                });
            }

            services.AddMvc(
                options => options.RespectBrowserAcceptHeader = true
            ).AddMvcOptions(options => {
                options.Filters.Add(new ActionFilter(Redis));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IDatabase Redis) {
            loggerFactory.AddConsole();
            loggerFactory.AddProvider(new ScreamingLoggerProvider(Redis, _ApplicationSettings));

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseStatusCodePagesWithReExecute("/Error/{0}");
            app.UseScreamingExceptions(Redis);

            app.UseAuthentication();


            if(env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            } else {
                app.UseExceptionHandler("/Home/Error");
            }


            app.UseMvc();
        }
    }
}
