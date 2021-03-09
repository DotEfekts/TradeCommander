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

namespace SpaceTraders_Client.Providers
{
    public class LocationProvider
    {
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly SpaceTradersUserInfo _userInfo;
        private readonly ShipsProvider _shipInfo;

        public LocationProvider(
            CommandHandler commandHandler,
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions,
            SpaceTradersUserInfo userInfo,
            ShipsProvider shipInfo
            )
        {
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;
            _userInfo = userInfo;
            _shipInfo = shipInfo;

            commandHandler.RegisterAsyncCommand("SCAN", HandleScanCommandAsync);
        }

        private async Task<CommandResult> HandleScanCommandAsync(string[] args)
        {
            if (_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return CommandResult.FAILURE;
            }

            if (args.Length == 0)
            {
                _console.WriteLine("Displaying systems list.");
                _navManager.NavigateTo(_navManager.BaseUri + "systems");
                return CommandResult.SUCCESS;
            }
            else if (args[0] == "?" || args[0].ToLower() == "help")
            {
                _console.WriteLine("SCAN: Provides functions for searching systems for locations.");
                _console.WriteLine("Usage: Displays details about the systems available - SCAN");
                _console.WriteLine("Subcommands");
                _console.WriteLine("map: Displays the map for the specified system - SCAN map <System Symbol/Ship Id>");
                _console.WriteLine("system: Displays the locations available within the specified system - SCAN system <System Symbol>");
                _console.WriteLine("location: Prints info about a specific location - SCAN location <Location Symbol>");
                return CommandResult.SUCCESS;
            }
            else if (args[0].ToLower() == "map" && args.Length == 2)
            {
                var ship = _shipInfo.GetShipDataByLocalId(args[1]);
                if (ship != null)
                {
                    _console.WriteLine("Displaying local map for " + ship.DisplayName + ".");
                    _navManager.NavigateTo(_navManager.BaseUri + "map/" + ship.ServerId);
                }
                else
                {
                    _console.WriteLine("Displaying system map for " + args[1].ToUpper() + ".");
                    _navManager.NavigateTo(_navManager.BaseUri + "map/" + args[1].ToUpper());
                }

                return CommandResult.SUCCESS;
            }
            else if(args[0].ToLower() == "system" && args.Length == 2)
            {
                _console.WriteLine("Displaying locations list for " + args[1].ToUpper() + ".");
                _navManager.NavigateTo(_navManager.BaseUri + "systems/" + args[1].ToUpper());
                return CommandResult.SUCCESS;
            }
            else if(args[0].ToLower() == "location" && args.Length == 2)
            {
                _console.WriteLine("Scanning location: " + args[1].ToUpper() + ".");
                try
                {
                    var locationInfo = await _http.GetFromJsonAsync<LocationResponse>("/game/locations/" + args[1].ToUpper(), _serializerOptions);
                    await _console.WriteLine("Symbol: " + locationInfo.Planet.Symbol, 0);
                    await _console.WriteLine("Type: " + locationInfo.Planet.Type, 100);
                    await _console.WriteLine("Name: " + locationInfo.Planet.Name, 100);
                    if(locationInfo.Planet.AnsibleProgress.HasValue)
                        await _console.WriteLine("Ansible Progress: " + locationInfo.Planet.AnsibleProgress, 100);
                    await _console.WriteLine("X: " + locationInfo.Planet.X, 100);
                    await _console.WriteLine("Y: " + locationInfo.Planet.Y, 100);

                    return CommandResult.SUCCESS;
                }
                catch (Exception)
                {
                    _console.WriteLine("Scan failed. (Does the location exist?)");
                }

                return CommandResult.FAILURE;
            }

            return CommandResult.INVALID;
        }
    }
}
