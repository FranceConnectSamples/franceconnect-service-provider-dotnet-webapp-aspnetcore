//
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using WebApp_Service_Provider_DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using WebApp_Service_Provider_DotNet.ViewModels.Data;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using IdentityModel.Client;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace WebApp_Service_Provider_DotNet.Controllers
{
    [Authorize]
    public class DataController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger _logger;
        private readonly FranceConnectConfiguration _config;

        public DataController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILoggerFactory loggerFactory,
            IOptions<FranceConnectConfiguration> config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = loggerFactory.CreateLogger<AccountController>();
            _config = config.Value;
        }

        //
        // GET: /Data/
        [HttpGet]
        public IActionResult Index()
        {
            return View(_config.DataProviders);
        }

        //
        // GET: /Data/GetResource
        [HttpGet]
        public IActionResult GetResource(string provider)
        {
            var state = Guid.NewGuid().ToString("N");
            var scope = new List<string> { "openid", "email" };
            scope.AddRange(_config.DataProviders.FirstOrDefault(dp => dp.Name == provider).Scopes);

            var json = JsonConvert.SerializeObject(new ConsentCookie
            {
                Provider = provider,
                State = state
            });
            Response.Cookies.Delete("consent");
            Response.Cookies.Append("consent", Base64Encode(json), new CookieOptions { Expires = DateTimeOffset.Now.AddMinutes(15) });

            var authorizeRequest = new RequestUrl(_config.AuthorizationEndpoint);
            return Redirect(authorizeRequest.CreateAuthorizeUrl(
                clientId: _config.ClientId,
                responseType: "code",
                scope: string.Join(" ", scope),
                redirectUri: GetConsentRedirectUri(),
                state: state,
                nonce: Guid.NewGuid().ToString("N")));
        }

        //
        // GET: /Data/ConsentCallback
        [HttpGet]
        public async Task<IActionResult> GetResourceCallback(string code, string state)
        {
            ConsentCookie consentCookie = null;
            string json;
            try
            {
                json = Base64Decode(Request.Cookies["consent"]);
                consentCookie = JsonConvert.DeserializeObject<ConsentCookie>(json);
                Response.Cookies.Delete("consent");
            }
            catch (Exception)
            {
                throw new Exception("Unable to retrieve cookie");
            }

            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentNullException("Authorization code cannot be null");
            }
            if (string.IsNullOrEmpty(state))
            {
                throw new ArgumentNullException("State cannot be null");
            }
            if (state != consentCookie.State)
            {
                throw new ArgumentException("Invalid state");
            }

            var tokenClient = new TokenClient(_config.TokenEndpoint, _config.ClientId, _config.ClientSecret, null,AuthenticationStyle.PostValues);
            var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(code, GetConsentRedirectUri());
            if (tokenResponse.IsError || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                throw new Exception("Unable to retrieve access token");
            }

            consentCookie.State = null;
            consentCookie.Token = tokenResponse.AccessToken;
            json = JsonConvert.SerializeObject(consentCookie);
            Response.Cookies.Append("consent", Base64Encode(json), new CookieOptions { Expires = DateTimeOffset.Now.AddMinutes(15) });

            return RedirectToAction(nameof(Resource));
        }

        //
        // GET: /Data/GetResourceCallback
        [HttpGet]
        public async Task<IActionResult> Resource()
        {
            ConsentCookie consentCookie = null;
            try
            {
                var json = Base64Decode(Request.Cookies["consent"]);
                consentCookie = JsonConvert.DeserializeObject<ConsentCookie>(json);
            }
            catch (Exception)
            {
                ViewData["Message"] = "Impossible d'obtenir l'autorisation d'accès aux ressources, veuillez réessayer.";
                return View();
            }

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", consentCookie.Token);
            var response = await client.GetAsync(GetResourceUrl(consentCookie.Provider));
            if (response.IsSuccessStatusCode)
            {
                var resource = await response.Content.ReadAsStringAsync();
                return View(ConvertResource(resource, consentCookie.Provider));
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ViewData["Message"] = "La ressource demandée n'a pas été trouvée.";
                return View();
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ViewData["Message"] = "Vous n'êtes pas autorisé à accéder cette ressource.";
                return View();
            }
            else
            {
                ViewData["Message"] = "Impossible de récupérer les données auprès du fournisseur choisi.";
                return View();
            }
        }

        private class ConsentCookie
        {
            public string Provider { get; set; }
            public string State { get; set; }
            public string Token { get; set; }
        }

        #region Helpers

        private BaseResourceViewModel ConvertResource(string json, string scheme)
        {
            switch (scheme)
            {
                case "DGFIP":
                    return JsonConvert.DeserializeObject<DgfipResourceViewModel>(json);
                case "Custom":
                    return JsonConvert.DeserializeObject<CustomResourceViewModel>(json);
                default:
                    throw new NotImplementedException();
            }
        }

        private string GetResourceUrl(string providerName)
        {
            DataProvider provider = _config.DataProviders.FirstOrDefault(dp => dp.Name == providerName);
            if (provider != null)
            {
                return provider.Endpoint;
            }
            else
            {
                throw new InvalidOperationException(string.Format("Unable to find \"{0}\" data provider", providerName));
            }
        }

        private string GetConsentRedirectUri()
        {
            return Request.Scheme + "://" + Request.Host + Url.Action(nameof(GetResourceCallback));
        }

        private string Base64Encode(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        private string Base64Decode(string base64EncodedText)
        {
            var bytes = Convert.FromBase64String(base64EncodedText);
            return Encoding.UTF8.GetString(bytes);
        }

        #endregion
    }
}