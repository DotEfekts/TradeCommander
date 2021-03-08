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
    public class AutoRouteProvider
    {
        private readonly ISyncLocalStorageService _localStorage;
        private readonly SpaceTradersUserInfo _userInfo;
        private readonly StateEvents _stateEvents;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        private Dictionary<int, AutoRoute> _routeData;
        
        public AutoRouteProvider(
            ISyncLocalStorageService localStorage,
            SpaceTradersUserInfo userInfo,
            StateEvents stateEvents,
            CommandHandler commandHandler,
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions
            )
        {
            _localStorage = localStorage;
            _userInfo = userInfo;
            _stateEvents = stateEvents;
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;

            stateEvents.StateChange += async (source, type) =>
            {
            };

            commandHandler.RegisterAsyncCommand("AUTO", HandleAutoCommandAsync);
        }

        private async Task HandleAutoCommandAsync(string[] args) 
        {
            if(_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return;
            }
            
            if(args.Length == 0)
            {
                _console.WriteLine("Invalid arguments. (See AUTO help)");
            }
            else if(args[0] == "?" || args[0].ToLower() == "help")
            {
                _console.WriteLine("AUTO: Provides functions for automatic routes.");
                _console.WriteLine("Subcommands");
                _console.WriteLine("list: Lists the command on the route - AUTO list <Route Id/Name>");
                _console.WriteLine("new: Creates a new auto route - AUTO new <Route Name>");
                _console.WriteLine("add: Adds a command to the end of the route. Adds at the position indicated if supplied. - AUTO add <Route Id/Name> [Index] <Command>");
                _console.WriteLine("remove: Removes a command from the route - AUTO remove <Route Id/Name> <Command Index>");
                _console.WriteLine("start: Start a ship running on the route - AUTO start <Ship Id/Name>");
                _console.WriteLine("stop: Stop a ship running the route - AUTO stop <Ship Id/Name>");
            }
            else if(args[0].ToLower() == "list")
            {

            }
            else
            {
                _console.WriteLine("Invalid arguments. (See AUTO help)");
            }
        }
    }
}
