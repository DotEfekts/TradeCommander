using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using SpaceTraders_Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace SpaceTraders_Client.Providers
{
    public class ShipyardProvider
    {
        private readonly SpaceTradersUserInfo _userInfo;
        private readonly StateEvents _stateEvents;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        
        public ShipyardProvider(
            SpaceTradersUserInfo userInfo,
            StateEvents stateEvents,
            CommandHandler commandHandler,
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions
            )
        {
            _userInfo = userInfo;
            _stateEvents = stateEvents;
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;

            commandHandler.RegisterAsyncCommand("SHIPYARD", HandleShipyardCommandAsync);
        }

        private async Task HandleShipyardCommandAsync(string[] args) 
        {
            if(_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return;
            }
            
            if(args.Length == 0)
            {
                _console.WriteLine("Invalid arguments. (See SHIPYARD help)");
            }
            else if(args[0] == "?" || args[0].ToLower() == "help")
            {
                _console.WriteLine("SHIPYARD: Provides functions for managing ships.");
                _console.WriteLine("Subcommands");
                _console.WriteLine("list: Shows all ships available to purchase - SHIPYARD list");
                _console.WriteLine("      Shows locations a ship is available to purchase - SHIPYARD list <Ship Type>");
                _console.WriteLine("buy: Purchase a ship - SHIPYARD buy <Ship Type> <Location Symbol>");
            }
            else if(args[0].ToLower() == "list")
            {
                if(args.Length > 2)
                    _console.WriteLine("Invalid arguments. (See SHIPYARD help)");
                else if(args.Length == 1)
                {
                    _console.WriteLine("Displaying ship list.");
                    _navManager.NavigateTo("/shipyard");
                }
                else
                {
                    _console.WriteLine("Displaying purchase location list.");
                    _navManager.NavigateTo("/shipyard/" + args[1].ToUpper());
                }
            }
            else if(args[0].ToLower() == "buy")
            {
                if (args.Length != 3)
                    _console.WriteLine("Invalid arguments. (See SHIPYARD help)");
                else
                {
                    using var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/ships", new ShipyardPurchaseRequest
                    {
                        Location = args[2].ToUpper(),
                        Type = args[1].ToUpper()
                    });

                    if (httpResult.StatusCode == HttpStatusCode.Created)
                    {
                        var details = await httpResult.Content.ReadFromJsonAsync<DetailsResponse>(_serializerOptions);

                        var cost = _userInfo.UserDetails.Credits - details.User.Credits;
                        _userInfo.UserDetails.Credits = details.User.Credits;
                        _console.WriteLine("Ship purchased successfully. Total cost: " + cost + " credits.");
                        _stateEvents.TriggerUpdate(this, "shipPurchased");
                    }
                    else
                    {
                        var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                        if (error.Error.Message == "Ship does not exist.")
                            _console.WriteLine("Invalid ship type provided. Please check the type and try again.");
                        else if(error.Error.Message == "Location does not exist.")
                            _console.WriteLine("Location does not exist. Please check location and try again.");
                        else if(error.Error.Message == "Ship is not available for purchase on this planet.")
                            _console.WriteLine("Ship is not available for purchase at this location.");
                        else if (error.Error.Message == "User has insufficient funds to purchase ship.")
                            _console.WriteLine("Insufficient credits available for purchase.");
                        else
                            _console.WriteLine(error.Error.Message);
                    }

                }
            }
            else
            {
                _console.WriteLine("Invalid arguments. (See SHIPYARD help)");
            }
        }
    }
}
