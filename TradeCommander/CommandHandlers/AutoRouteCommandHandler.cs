using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using System;
using System.Linq;
using System.Threading.Tasks;
using TradeCommander.Models;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class AutoRouteCommandHandler : ICommandHandler
    {
        private readonly ShipsProvider _shipInfo;
        private readonly AutoRouteProvider _routeInfo;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;

        public AutoRouteCommandHandler(
            ShipsProvider shipInfo,
            AutoRouteProvider routeInfo,
            ConsoleOutput console,
            NavigationManager navManager
            )
        {
            _shipInfo = shipInfo;
            _routeInfo = routeInfo;
            _console = console;
            _navManager = navManager;
        }

        public string CommandName => "AUTO";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => true;

        public CommandResult HandleCommand(string[] args, bool background, bool loggedIn)
        {
            if (args.Length == 0 && (args[0] == "?" || args[0].ToLower() == "help"))
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
            else if (args.Length > 0 && args[0].ToLower() == "list")
            {
                if (args.Length == 1)
                {
                    _console.WriteLine("Displaying auto route list.");
                    _navManager.NavigateTo(_navManager.BaseUri + "routes");
                    return CommandResult.SUCCESS;
                }
                else if (args.Length == 3 && (args[1].ToLower() == "commands" || args[1].ToLower() == "ships"))
                {
                    _console.WriteLine("Displaying auto route commands.");
                    _navManager.NavigateTo(_navManager.BaseUri + "routes/" + args[1].ToLower() + "/" + args[2]);
                    return CommandResult.SUCCESS;
                }
            }
            else if (args.Length == 2 && args[0].ToLower() == "new")
            {
                var id = _routeInfo.GetRouteData().Length + 1;
                _routeInfo.AddRoute(id, new AutoRoute
                {
                    Id = id,
                    DisplayName = args[1],
                    Commands = Array.Empty<RouteCommand>(),
                    Ships = Array.Empty<RouteShip>()
                });

                _console.WriteLine("New route created. Id: " + id + ".");
                _navManager.NavigateTo(_navManager.BaseUri + "routes");

                return CommandResult.SUCCESS;
            }
            else if (args.Length == 2 && args[0].ToLower() == "delete")
            {
                if (int.TryParse(args[1], out int id) && _routeInfo.TryGetRoute(id, out AutoRoute route))
                {
                    _routeInfo.DeleteRoute(route);
                    _console.WriteLine("Auto route deleted.");
                    _navManager.NavigateTo(_navManager.BaseUri + "routes");

                    return CommandResult.SUCCESS;
                }
                else
                    _console.WriteLine("Invalid route id provided.");

                return CommandResult.FAILURE;
            }
            else if (args.Length >= 3 && args[0].ToLower() == "add")
            {
                if (int.TryParse(args[1], out int id) && _routeInfo.TryGetRoute(id, out AutoRoute route))
                {
                    var argsAdjust = int.TryParse(args[2], out int index) ? 3 : 2;

                    if (argsAdjust == 2)
                        index = route.Commands.Length;
                    else
                        index -= 1;

                    index = Math.Max(index, 0);
                    index = Math.Min(index, route.Commands.Length);

                    var commandArr = new string[args.Length - argsAdjust];
                    Array.Copy(args, argsAdjust, commandArr, 0, args.Length - argsAdjust);

                    _routeInfo.AddCommandToRoute(route.Id, index, string.Join(' ', commandArr));

                    _navManager.NavigateTo(_navManager.BaseUri + "routes/commands/" + id);
                    _console.WriteLine("Command added successfully.");

                    return CommandResult.SUCCESS;
                }
                else
                    _console.WriteLine("Invalid route id provided.");

                return CommandResult.FAILURE;
            }
            else if (args.Length == 3 && args[0].ToLower() == "remove")
            {
                if (int.TryParse(args[1], out int id) && _routeInfo.TryGetRoute(id, out AutoRoute route))
                {
                    if (int.TryParse(args[2], out int index) && index > 0 && index <= route.Commands.Length)
                    {
                        _routeInfo.RemoveCommandFromRoute(route.Id, index);
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
            else if (args.Length == 3 && args[0].ToLower() == "start")
            {
                if (int.TryParse(args[1], out int id) && _routeInfo.TryGetRoute(id, out AutoRoute route))
                {
                    if (_shipInfo.TryGetShipDataByLocalId(args[2], out var shipData))
                    {
                        var currentRoute = _routeInfo.GetShipRoute(shipData.ServerId);
                        if (currentRoute == null || currentRoute == route)
                        {
                            if (currentRoute == null)
                            {
                                _routeInfo.AddShipToRoute(route.Id, shipData);
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
            else if (args.Length == 3 && args[0].ToLower() == "stop")
            {
                if (int.TryParse(args[1], out int id) && _routeInfo.TryGetRoute(id, out AutoRoute route))
                {
                    if (_shipInfo.TryGetShipDataByLocalId(args[2], out var shipData))
                    {
                        var routeShip = route.Ships.FirstOrDefault(t => t.ShipId == shipData.ServerId);
                        if (routeShip != null)
                        {
                            _routeInfo.RemoveShipFromRoute(route.Id, routeShip);
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

            return CommandResult.INVALID;
        }
    }
}
