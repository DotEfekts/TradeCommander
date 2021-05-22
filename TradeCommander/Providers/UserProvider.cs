using Blazored.LocalStorage;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeCommander.Models;

namespace TradeCommander.Providers
{
    public class UserProvider
    {
        public string Token { get; private set; }
        public string Username { get; private set; }
        public bool StartingDetailsChecked { get; private set; } = false;
        public User UserDetails { get; private set; }

        private readonly ISyncLocalStorageService _localStorage;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly IConfiguration _config;

        public event EventHandler<UserEventArgs> UserUpdated;

        public UserProvider(
            ISyncLocalStorageService localStorage,
            HttpClient http,
            JsonSerializerOptions serializerOptions,
            IConfiguration config
            )
        {
            _localStorage = localStorage;
            _http = http;
            _serializerOptions = serializerOptions;
            _config = config;

            if (_localStorage.ContainKey("Token"))
                _ = SetDetailsAsync(_localStorage.GetItemAsString("Token"));
            else
                StartingDetailsChecked = true;
        }

        public void SetCredits(int credits)
        {
            UserDetails.Credits = credits;

            UserUpdated?.Invoke(this, new UserEventArgs
            {
                UserDetails = UserDetails,
                IsFullRefresh = false,
                IsInitialCheck = false
            });
        }

        public async Task<bool> SetDetailsAsync(string token)
        {
            var initialCheck = !StartingDetailsChecked;

            try
            {
                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(_config["base_url"] + "/my/account"),
                    Method = HttpMethod.Get
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var httpResponse = await _http.SendAsync(request);
                var userResponse = await httpResponse.Content.ReadFromJsonAsync<DetailsResponse>(_serializerOptions);

                if (userResponse != null)
                {
                    Username = userResponse.User.Username;
                    UserDetails = userResponse.User;
                    Token = token;

                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    _localStorage.SetItem("Token", Token);

                    StartingDetailsChecked = true;
                    UserUpdated?.Invoke(this, new UserEventArgs
                    {
                        UserDetails = userResponse.User,
                        IsFullRefresh = true,
                        IsInitialCheck = initialCheck
                    });

                    return true;
                }
            }
            catch (Exception) { }

            if(initialCheck)
                UserUpdated?.Invoke(this, new UserEventArgs
                {
                    UserDetails = null,
                    IsFullRefresh = true,
                    IsInitialCheck = initialCheck
                });

            StartingDetailsChecked = true;
            return false;
        }

        public void Logout()
        {
            UserDetails = null;
            Username = null;
            Token = null;
            _localStorage.RemoveItem("Token");

            UserUpdated?.Invoke(this, new UserEventArgs
            {
                UserDetails = null,
                IsFullRefresh = true,
                IsInitialCheck = false
            });
        }

        public async Task RefreshData()
        {
            var userResponse = await _http.GetFromJsonAsync<DetailsResponse>("/my/account", _serializerOptions);

            Username = userResponse.User.Username;
            UserDetails = userResponse.User;

            UserUpdated?.Invoke(this, new UserEventArgs
            {
                UserDetails = userResponse.User,
                IsFullRefresh = true,
                IsInitialCheck = false
            });
        }
    }

    public class UserEventArgs
    {
        public User UserDetails { get; set; }
        public bool IsFullRefresh { get; set; }
        public bool IsInitialCheck { get; set; }
    }
}
