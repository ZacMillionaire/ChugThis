using System;
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

namespace Nulah.ChugThis {
    public class Startup {
        private IConfiguration Configuration { get; }
        private AppSettings _ApplicationSettings { get; set; }


        public Startup(IConfiguration configuration) {
            Configuration = configuration;
            _ApplicationSettings = new AppSettings();
            Configuration.Bind(_ApplicationSettings);

            if(_ApplicationSettings.ConnectionStrings.Redis.BaseKey.EndsWith(':')) {
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
                        },
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
