using Blazored.LocalStorage;
using SpaceTraders_Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpaceTraders_Client
{
    public class SpaceTradersUserInfo
    {
        public string Token { get; private set; }
        public string Username { get; private set; }
        public bool StartingDetailsChecked { get; private set; } = false;
        public User UserDetails { get; private set; }

        private readonly ISyncLocalStorageService _localStorage;
        private readonly HttpClient _http;
        private readonly ConsoleOutput _console;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly StateEvents _uiEvents;

        public SpaceTradersUserInfo(
            ISyncLocalStorageService localStorage,
            HttpClient http,
            ConsoleOutput console,
            CommandHandler commandHandler,
            JsonSerializerOptions serializerOptions,
            StateEvents uiEvents
            )
        {
            _localStorage = localStorage;
            _http = http;
            _console = console;
            _serializerOptions = serializerOptions;
            _uiEvents = uiEvents;


            if (_localStorage.ContainKey("Username") && _localStorage.ContainKey("Token"))
                _ = SetDetailsAsync(_localStorage.GetItemAsString("Username"), _localStorage.GetItemAsString("Token"), true);
            else
                StartingDetailsChecked = true;

            commandHandler.RegisterAsyncCommand("LOGIN", HandleLoginAsync);
            commandHandler.RegisterAsyncCommand("SIGNUP", HandleSignupAsync);
            commandHandler.RegisterCommand("LOGOUT", HandleLogout);
        }

        public async Task ValidateDetailsAsync(bool initialDetails)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<DetailsResponse>("/users/" + Username, _serializerOptions);

                if (response != null)
                {
                    UserDetails = response.User;
                    _localStorage.SetItem("Token", Token);
                    _localStorage.SetItem("Username", Username);

                    if(!initialDetails)
                        _uiEvents.TriggerUpdate(this, "userLogin");
                }
            }
            catch (Exception) { }
            finally
            {
                StartingDetailsChecked = true;
                _uiEvents.TriggerUpdate(this, "userChecked");
            }
        }

        public async Task SetDetailsAsync(string username, string token, bool initialDetails)
        {
            Username = username;
            Token = token;

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

            if (Token != null && Username != null)
                await ValidateDetailsAsync(initialDetails);
        }

        private async Task HandleLoginAsync(string[] args)
        {
            if (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("LOGIN: Logs an existing user into the SpaceTraders API.");
                _console.WriteLine("Usage: LOGIN <Username> <Token>");
            }
            else if(args.Length != 2)
            {
                _console.WriteLine("Invalid arguments. (See LOGIN help)");
            }
            else
            {
                await SetDetailsAsync(args[0], args[1], false);
                if(UserDetails != null)
                {
                    _console.Clear();
                    _console.WriteLine("Welcome back, " + UserDetails.Username + ".");
                }
                else
                {
                    _console.WriteLine("Incorrect login details. Please try again.");
                }
            }
        }

        private void HandleLogout(string[] args)
        {
            if (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("LOGOUT: Logs current user out of the SpaceTraders API.");
                _console.WriteLine("Usage: LOGOUT");
            }
            else if (args.Length > 0)
            {
                _console.WriteLine("Invalid arguments. (See LOGOUT help)");
            }
            else
            {
                UserDetails = null;
                Username = null;
                Token = null;
                _localStorage.RemoveItem("Token");
                _localStorage.RemoveItem("Username");

                _console.Clear();
                _console.WriteLine("Goodbye.");
                _uiEvents.TriggerUpdate(this, "userLogout");
            }
        }

        private async Task HandleSignupAsync(string[] args)
        {
            if (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("SIGNUP: Creates an account for the SpaceTraders API.");
                _console.WriteLine("Usage: SIGNUP <Username>");
            }
            else if (args.Length != 1)
            {
                _console.WriteLine("Invalid arguments. (See SIGNUP help)");
            }
            else
            {
                var httpResult = await _http.PostAsJsonAsync("/users/" + args[0] + "/token", new { });

                if (httpResult.StatusCode == HttpStatusCode.Created)
                {
                    var signupResult = await httpResult.Content.ReadFromJsonAsync<SignupResponse>(_serializerOptions);

                    await SetDetailsAsync(signupResult.User.Username, signupResult.Token, false);

                    _console.Clear();
                    _console.WriteLine("Welcome, " + UserDetails.Username + ". Your token is: " + signupResult.Token);
                    _console.WriteLine("Please copy this token somewhere safe as it is not recoverable.");

                    if (UserDetails == null)
                        _console.WriteLine("An error occurred during login. Please copy your token and login manually.");
                }
                else if (httpResult.StatusCode == HttpStatusCode.Conflict) 
                {
                    _console.WriteLine("Username already exists. Please pick another.");
                }
                else
                {
                    var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                    _console.WriteLine(error.Error.Message);
                }
            }
        }
    }
}
