using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using TradeCommander.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace TradeCommander.CommandHandlers
{
    public class ScanCommandHandler : ICommandHandlerAsync
    {
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;

        public ScanCommandHandler(
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions
            )
        {
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;
        }

        public string CommandName => "SCAN";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => true;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public async Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn)
        {
            if (args.Length == 0)
            {
                _console.WriteLine("Displaying systems list.");
                _navManager.NavigateTo(_navManager.BaseUri + "systems");
                return CommandResult.SUCCESS;
            }
            else if (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("SCAN: Provides functions for searching systems for locations.");
                _console.WriteLine("Usage: Displays details about the systems available - SCAN");
                _console.WriteLine("Subcommands");
                _console.WriteLine("map: Displays the map for the specified system - SCAN map <System Symbol>");
                _console.WriteLine("system: Displays the locations available within the specified system - SCAN system <System Symbol>");
                _console.WriteLine("location: Prints info about a specific location - SCAN location <Location Symbol>");
                return CommandResult.SUCCESS;
            }
            else if (args.Length == 2 && args[0].ToLower() == "map")
            {
                _console.WriteLine("Displaying system map for " + args[1].ToUpper() + ".");
                _navManager.NavigateTo(_navManager.BaseUri + "map/" + args[1].ToUpper());

                return CommandResult.SUCCESS;
            }
            else if(args.Length == 2 && args[0].ToLower() == "system")
            {
                _console.WriteLine("Displaying locations list for " + args[1].ToUpper() + ".");
                _navManager.NavigateTo(_navManager.BaseUri + "systems/" + args[1].ToUpper());
                return CommandResult.SUCCESS;
            }
            else if(args.Length == 2 && args[0].ToLower() == "location")
            {
                _console.WriteLine("Scanning location: " + args[1].ToUpper() + ".");
                try
                {
                    var locationInfo = await _http.GetFromJsonAsync<LocationResponse>("/locations/" + args[1].ToUpper(), _serializerOptions);
                    await _console.WriteLine("Symbol: " + locationInfo.Location.Symbol, 0);
                    await _console.WriteLine("Type: " + locationInfo.Location.Type, 100);
                    await _console.WriteLine("Name: " + locationInfo.Location.Name, 100);
                    if(locationInfo.Location.AnsibleProgress.HasValue)
                        await _console.WriteLine("Ansible Progress: " + locationInfo.Location.AnsibleProgress, 100);
                    if (!string.IsNullOrEmpty(locationInfo.Location.Anomaly))
                        await _console.WriteLine("Anomaly Data: " + locationInfo.Location.Anomaly, 100);
                    await _console.WriteLine("X: " + locationInfo.Location.X, 100);
                    await _console.WriteLine("Y: " + locationInfo.Location.Y, 100);

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
