using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using TradeCommander.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;

namespace TradeCommander.Providers
{
    public class AutoRouteProvider
    {
        private readonly ISyncLocalStorageService _localStorage;
        private readonly SpaceTradersUserInfo _userInfo;
        private readonly ShipsProvider _shipInfo;
        private readonly StateEvents _stateEvents;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly CommandHandler _commandHandler;

        private readonly SemaphoreSlim updateLock = new SemaphoreSlim(1, 1);

        internal Dictionary<int, AutoRoute> RouteData { get; private set; }
        
        private const int ROUTE_UPDATE_INTERVAL = 1000;

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
            _commandHandler = commandHandler;

            stateEvents.StateChange += (source, type) =>
            {
                if (type == "shipsRefreshed" || type == "userChecked" || type == "userLogout")
                    LoadRouteData();
            };

            LoadRouteData();
            StartRouteRunner();

            commandHandler.RegisterCommand("AUTO", HandleAutoCommand);
        }

        private void RelinkShipData()
        {
            if(RouteData != null && _shipInfo.HasShips())
            {
                foreach (var route in RouteData)
                    foreach (var routeShip in route.Value.Ships)
                        routeShip.ShipData = _shipInfo.GetReferencedShipData(routeShip.ShipId);
                foreach (var route in RouteData)
                    route.Value.Ships = route.Value.Ships.Where(t => t.ShipData != null).ToArray();
            }
        }

        private void StartRouteRunner()
        {
            var timer = new System.Timers.Timer(ROUTE_UPDATE_INTERVAL);
            timer.Elapsed += UpdateRoutes;
            timer.Enabled = true;
        }

        private async void UpdateRoutes(object sender, ElapsedEventArgs args)
        {
            if(await updateLock.WaitAsync(0))
            {
                try
                {
                    if (RouteData != null)
                        foreach (var route in RouteData.Values)
                            foreach (var routeShip in route.Ships)
                                if (routeShip.ShipData.Ship.Location != null)
                                {
                                    RouteCommand command = null;
                                    do
                                    {
                                        if (routeShip.LastCommand > route.Commands.Max(t => t.Index))
                                            routeShip.LastCommand = 0;
                                        else
                                            routeShip.LastCommand++;

                                        command = route.Commands.FirstOrDefault(t => t.Index == routeShip.LastCommand);
                                    }
                                    while (command == null);

                                    var result = await _commandHandler.HandleCommand(command.Command.Replace("$s", routeShip.ShipData.Id.ToString()), true);
                                    if (result != CommandResult.SUCCESS)
                                    {
                                        var newShips = route.Ships.ToList();
                                        newShips.Remove(routeShip);
                                        route.Ships = newShips.ToArray();

                                        _console.WriteLine("Command failed for " + routeShip.ShipData.DisplayName + " during route. Ship removed from route.");
                                        _console.WriteLine("Command failed: " + command.Command);

                                        Console.Out.WriteLine("Removed ship.");
                                    }

                                }

                    SaveRouteData();
                    _stateEvents.TriggerUpdate(this, "routesUpdated");
                }
                finally
                {
                    updateLock.Release();
                }
            }
        }

        private CommandResult HandleAutoCommand(string[] args, bool background) 
        {
            if (_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return CommandResult.FAILURE;
            }

            if (background)
            {
                _console.WriteLine("This command cannot be run automatically.");
                return CommandResult.FAILURE;
            }

            if (args.Length > 0)
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
                    _console.WriteLine("     Use $s to provide a ship id for a command.");
                    _console.WriteLine("remove: Removes a command from the route - AUTO remove <Route Id> <Command Index>");
                    _console.WriteLine("start: Start a ship running on the route - AUTO start <Route Id> <Ship Id/Name>");
                    _console.WriteLine("stop: Stop a ship running the route - AUTO stop <Route Id> <Ship Id/Name>");
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
                    _stateEvents.TriggerUpdate(this, "routeAdded");
                    _console.WriteLine("New route created. Id: " + id + ".");
                    _navManager.NavigateTo(_navManager.BaseUri + "routes");

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

                        _stateEvents.TriggerUpdate(this, "routeDeleted");
                        _console.WriteLine("Auto route deleted.");
                        _navManager.NavigateTo(_navManager.BaseUri + "routes");

                        return CommandResult.SUCCESS;
                    }
                    else
                        _console.WriteLine("Invalid route id provided.");

                    return CommandResult.FAILURE;
                }
                else if (args[0].ToLower() == "add" && args.Length >= 3)
                {
                    if(int.TryParse(args[1], out int id) && RouteData.TryGetValue(id, out AutoRoute route))
                    {
                        var argsAdjust = int.TryParse(args[2], out int index) ? 3 : 2;

                        if(argsAdjust == 2)
                            index = route.Commands.Length;
                        else
                            index -= 1;

                        index = Math.Max(index, 0);
                        index = Math.Min(index, route.Commands.Length);

                        var list = route.Commands.ToList();
                        list.Where(c => c.Index >= index).ToList().ForEach(c => c.Index++);

                        var commandArr = new string[args.Length - argsAdjust];
                        Array.Copy(args, argsAdjust, commandArr, 0, args.Length - argsAdjust);

                        list.Insert(index, new RouteCommand
                        {
                            Index = index,
                            Command = string.Join(' ', commandArr),
                        });

                        route.Commands = list.ToArray();
                        SaveRouteData();

                        _stateEvents.TriggerUpdate(this, "routeCommandAdded");
                        _navManager.NavigateTo(_navManager.BaseUri + "routes/commands/" + id);
                        _console.WriteLine("Command added successfully.");

                        return CommandResult.SUCCESS;
                    }
                    else
                        _console.WriteLine("Invalid route id provided.");

                    return CommandResult.FAILURE;
                }
                else if (args[0].ToLower() == "remove" && args.Length == 3)
                {
                    if (int.TryParse(args[1], out int id) && RouteData.TryGetValue(id, out AutoRoute route))
                    {
                        if (int.TryParse(args[2], out int index) && index > 0 && index <= route.Commands.Length)
                        {
                            index -= 1;
                            index = Math.Max(index, 0);

                            var newCommands = route.Commands.ToList();
                            newCommands.Remove(newCommands.First(t => t.Index == index));
                            newCommands.Where(r => r.Index > index).ToList().ForEach(r => r.Index--);
                            route.Commands = newCommands.ToArray();
                            SaveRouteData();

                            _stateEvents.TriggerUpdate(this, "routeCommandDeleted");
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
                else if (args[0].ToLower() == "start" && args.Length == 3)
                {
                    if (int.TryParse(args[1], out int id) && RouteData.TryGetValue(id, out AutoRoute route))
                    {
                        var shipData = _shipInfo.GetShipDataByLocalId(args[2]);
                        if (shipData != null)
                        {
                            var currentRoute = RouteData.Select(r => r.Value).FirstOrDefault(r => r.Ships.Any(s => s.ShipId == shipData.ServerId));
                            if (currentRoute == null || currentRoute == route)
                            {
                                if (currentRoute == null)
                                {
                                    var newShips = route.Ships.ToList();
                                    newShips.Add(new RouteShip
                                    {
                                        LastCommand = -1,
                                        ShipData = shipData,
                                        ShipId = shipData.ServerId
                                    });
                                    route.Ships = newShips.ToArray();

                                    SaveRouteData();

                                    _stateEvents.TriggerUpdate(this, "routeShipAdded");
                                    _console.WriteLine("Ship added to route.");
                                    _navManager.NavigateTo(_navManager.BaseUri + "routes/ships/" + id);
                                }
                                else
                                    _console.WriteLine("Ship provided is already on route.");
                                return CommandResult.SUCCESS;
                            }
                            else
                                _console.WriteLine("Ship is already on another route. Please remove ship from current route before adding it to a new one.");
                        }
                        else
                            _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                    }
                    else
                        _console.WriteLine("Invalid route id provided.");
                    return CommandResult.FAILURE;
                }
                else if (args[0].ToLower() == "stop" && args.Length == 3)
                {
                    if (int.TryParse(args[1], out int id) && RouteData.TryGetValue(id, out AutoRoute route))
                    {
                        var shipData = _shipInfo.GetShipDataByLocalId(args[2]);
                        if (shipData != null)
                        {
                            var routeShip = route.Ships.FirstOrDefault(t => t.ShipId == shipData.ServerId);
                            if (routeShip != null)
                            {
                                var newShips = route.Ships.ToList();
                                newShips.Remove(routeShip);
                                route.Ships = newShips.ToArray();

                                SaveRouteData();

                                _stateEvents.TriggerUpdate(this, "routeShipDeleted");
                                _console.WriteLine("Ship removed from route.");
                                _navManager.NavigateTo(_navManager.BaseUri + "routes/ships/" + id);
                            }
                            else
                                _console.WriteLine("Ship provided is not currently on this route.");
                            return CommandResult.SUCCESS;
                        }
                        else
                            _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
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
            if (_userInfo.UserDetails != null && !_shipInfo.DataRefreshing)
            {
                if (_localStorage.ContainKey("RouteData." + _userInfo.Username))
                    RouteData = _localStorage.GetItem<Dictionary<int, AutoRoute>>("RouteData." + _userInfo.Username);
                RouteData ??= new Dictionary<int, AutoRoute>();

                RelinkShipData();
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
