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
        private readonly ShipsProvider _shipInfo;
        private readonly StateEvents _stateEvents;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        
        internal Dictionary<int, AutoRoute> RouteData { get; private set; }
        
        public AutoRouteProvider(
            ISyncLocalStorageService localStorage,
            SpaceTradersUserInfo userInfo,
            ShipsProvider shipInfo,
            StateEvents stateEvents,
            CommandHandler commandHandler,
            ConsoleOutput console,
            NavigationManager navManager
            )
        {
            _localStorage = localStorage;
            _userInfo = userInfo;
            _shipInfo = shipInfo;
            _stateEvents = stateEvents;
            _console = console;
            _navManager = navManager;

            stateEvents.StateChange += (source, type) =>
            {
                if (type == "userChecked" || type == "userLogout")
                    LoadRouteData();
            };

            commandHandler.RegisterCommand("AUTO", HandleAutoCommand);
        }

        private CommandResult HandleAutoCommand(string[] args) 
        {
            if(_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return CommandResult.FAILURE;
            }
            
            if(args.Length > 0)
            {
                if(args[0] == "?" || args[0].ToLower() == "help")
                {
                    _console.WriteLine("AUTO: Provides functions for automatic routes.");
                    _console.WriteLine("Subcommands");
                    _console.WriteLine("list: Lists all routes created - AUTO list");
                    _console.WriteLine("    - Lists the commands on the route - AUTO list commands <Route Id>");
                    _console.WriteLine("    - Lists the ships assigned to the route - AUTO list ships <Route Id>");
                    _console.WriteLine("new: Creates a new auto route - AUTO new <Route Name>");
                    _console.WriteLine("delete: Deletes an existing auto route - AUTO delete <Route Id>");
                    _console.WriteLine("add: Adds a command to the end of the route. Adds at the position indicated if supplied. - AUTO add <Route Id> [Index] <Command>");
                    _console.WriteLine("remove: Removes a command from the route - AUTO remove <Route Id> <Command Index>");
                    _console.WriteLine("start: Start a ship running on the route - AUTO start <Ship Id/Name>");
                    _console.WriteLine("stop: Stop a ship running the route - AUTO stop <Ship Id/Name>");
                    return CommandResult.SUCCESS;
                }
                else if(args[0].ToLower() == "list")
                {
                    if(args.Length == 1)
                    {
                        _console.WriteLine("Displaying auto route list.");
                        _navManager.NavigateTo(_navManager.BaseUri + "routes");
                        return CommandResult.SUCCESS;
                    }
                    else if(args.Length == 3 && (args[1].ToLower() == "commands" || args[1].ToLower() == "ships"))
                    {
                        _console.WriteLine("Displaying auto route commands.");
                        _navManager.NavigateTo(_navManager.BaseUri + "routes/" + args[1].ToLower() + "/" + args[2]);
                        return CommandResult.SUCCESS;
                    }
                }
                else if(args[0].ToLower() == "new" && args.Length == 2)
                {
                    var id = RouteData.Count + 1;
                    RouteData.Add(id, new AutoRoute
                    {
                        Id = id,
                        DisplayName = args[1],
                        Commands = Array.Empty<RouteCommand>(),
                        Ships = Array.Empty<RouteShip>()
                    });

                    SaveRouteData();
                    _console.WriteLine("New route created. Id: " + id + ".");

                    return CommandResult.SUCCESS;
                }
                else if (args[0].ToLower() == "delete" && args.Length == 2)
                {
                    if (int.TryParse(args[1], out int id) && RouteData.TryGetValue(id, out AutoRoute route))
                    {
                        var newRouteData = RouteData.Values.ToList();
                        newRouteData.Remove(route);
                        newRouteData.Where(r => r.Id > id).ToList().ForEach(r => r.Id--);
                        RouteData = newRouteData.ToDictionary(r => r.Id);
                        SaveRouteData();

                        _console.WriteLine("Auto route deleted.");
                    }
                    else
                        _console.WriteLine("Invalid route id provided.");
                }
                else if (args[0].ToLower() == "add" && (args.Length == 3 || args.Length == 4))
                {
                    if(int.TryParse(args[1], out int id) && RouteData.TryGetValue(id, out AutoRoute route))
                    {
                        var index = route.Commands.Length + 1;
                        if(args.Length == 3 || int.TryParse(args[2], out index))
                        {
                            index -= 1;
                            index = Math.Max(index, 0);
                            index = Math.Min(index, route.Commands.Length);

                            var list = route.Commands.ToList();
                            list.Where(c => c.Index >= index).ToList().ForEach(c => c.Index++);
                            list.Insert(index - 1, new RouteCommand
                            {
                                Index = index - 1,
                                Command = args.Length == 3 ? args[2] : args[3],
                            });

                            route.Commands = list.ToArray();
                            SaveRouteData();

                            _navManager.NavigateTo(_navManager.BaseUri + "routes/commands/" + id);
                            _console.WriteLine("Command added successfully.");
                            return CommandResult.SUCCESS;
                        }
                        else
                            _console.WriteLine("Invalid command index provided.");
                    }
                    else
                        _console.WriteLine("Invalid route id provided.");

                    return CommandResult.FAILURE;
                }
                else if (args[0].ToLower() == "remove" && args.Length == 3)
                {
                    if (int.TryParse(args[1], out int id) && RouteData.TryGetValue(id, out AutoRoute route))
                    {
                        if (int.TryParse(args[2], out int index) && route.Commands.Length <= index)
                        {
                            index -= 1;
                            index = Math.Max(index, 0);

                            var newCommands = route.Commands.ToList();
                            newCommands.Remove(newCommands.First(t => t.Index == index));
                            newCommands.Where(r => r.Index > index).ToList().ForEach(r => r.Index--);
                            route.Commands = newCommands.ToArray();
                            SaveRouteData();

                            _navManager.NavigateTo(_navManager.BaseUri + "routes/commands/" + id);
                            _console.WriteLine("Command deleted successfully.");
                            return CommandResult.SUCCESS;
                        }
                        else
                            _console.WriteLine("Invalid command index provided.");
                    }
                    else
                        _console.WriteLine("Invalid route id provided.");

                    return CommandResult.FAILURE;
                }
            }

            return CommandResult.INVALID;
        }

        private void LoadRouteData()
        {
            if (_userInfo.UserDetails != null && _shipInfo.HasShips())
            {
                if (_localStorage.ContainKey("RouteData." + _userInfo.Username))
                    RouteData = _localStorage.GetItem<Dictionary<int, AutoRoute>>("RouteData." + _userInfo.Username);
                RouteData ??= new Dictionary<int, AutoRoute>();

                foreach (var route in RouteData)
                {
                    foreach (var routeShip in route.Value.Ships)
                    {
                        var ship = _shipInfo.GetShipData(routeShip.ShipId);
                        if(ship != null)
                            routeShip.Ship = _shipInfo.GetShipData(routeShip.ShipId);
                    }

                    route.Value.Ships = route.Value.Ships.Where(t => t.Ship != null).ToArray();
                }

                SaveRouteData();
            }
            else
            {
                RouteData = null;
            }

            _stateEvents.TriggerUpdate(this, "routesRefreshed");
        }

        private void SaveRouteData()
        {
            if (RouteData != null)
                _localStorage.SetItem("RouteData." + _userInfo.Username, RouteData);
        }
    }
}
