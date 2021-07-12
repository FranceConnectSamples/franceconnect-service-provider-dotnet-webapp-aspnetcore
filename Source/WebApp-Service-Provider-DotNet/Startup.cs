﻿//
// The MIT License (MIT)
// Copyright (c) 2016 Microsoft France
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// You may obtain a copy of the License at https://opensource.org/licenses/MIT
//

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using WebApp_Service_Provider_DotNet.Models;
using WebApp_Service_Provider_DotNet.Services;

namespace WebApp_Service_Provider_DotNet
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            // Configuration loads behind the scenes since 2.0, with sources defined in program.cs https://docs.microsoft.com/en-us/aspnet/core/migration/1x-to-2x/?view=aspnetcore-3.1#add-configuration-providers
        }

        public IConfiguration Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            // Add configuration
            services.AddOptions();
            services.Configure<FranceConnectConfiguration>(Configuration.GetSection("FranceConnect"));
            var franceConnectConfig = Configuration.GetSection("FranceConnect").Get<FranceConnectConfiguration>();

            services.AddAuthentication(
                options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = Scheme.FranceConnect;
                })
                .AddCookie()
                .AddOpenIdConnect(Scheme.FranceConnect, Scheme.FranceConnectDisplayName, options => ConfigureFranceConnect(options, franceConnectConfig));

            services.AddControllersWithViews();

            // Add application services.
            services.AddTransient<IEmailSender, AuthMessageSender>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseRequestLocalization(new RequestLocalizationOptions { DefaultRequestCulture = new RequestCulture("fr-FR") });

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // To configure external authentication please see http://go.microsoft.com/fwlink/?LinkID=532715

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
        private void ConfigureFranceConnect(OpenIdConnectOptions oidc_options, FranceConnectConfiguration fcConfig)
        {

            oidc_options.ClientId = fcConfig.ClientId;
            oidc_options.ClientSecret = fcConfig.ClientSecret;
            oidc_options.CallbackPath = fcConfig.CallbackPath;
            oidc_options.SignedOutCallbackPath = fcConfig.SignedOutCallbackPath;
            oidc_options.Authority = fcConfig.Issuer;
            oidc_options.ResponseType = OpenIdConnectResponseType.Code;
            oidc_options.Scope.Clear();
            oidc_options.Scope.Add("openid");
            oidc_options.Scope.Add("profile");
            oidc_options.Scope.Add("birth");
            oidc_options.Scope.Add("email");
            oidc_options.GetClaimsFromUserInfoEndpoint = true;
            oidc_options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(fcConfig.ClientSecret));
            oidc_options.Configuration = new OpenIdConnectConfiguration
            {
                Issuer = fcConfig.Issuer,
                AuthorizationEndpoint = fcConfig.AuthorizationEndpoint + "?acr_values=" + fcConfig.EIdas,
                TokenEndpoint = fcConfig.TokenEndpoint,
                UserInfoEndpoint = fcConfig.UserInfoEndpoint,
                EndSessionEndpoint = fcConfig.EndSessionEndpoint,
            };

        }
    }
}
