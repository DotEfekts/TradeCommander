using Blazored.LocalStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TradeCommander.CommandHandlers;
using TradeCommander.Models;

namespace TradeCommander.Providers
{
    public class AutoRouteProvider
    {
        private readonly ISyncLocalStorageService _localStorage;
        private readonly UserProvider _userProvider;
        private readonly ShipsProvider _shipProvider;
        private readonly ConsoleOutput _console;
        private readonly CommandManager _commandManager;
        private readonly SemaphoreSlim updateLock = new SemaphoreSlim(1, 1);

        private Dictionary<int, AutoRoute> _routeData;

        private const int ROUTE_UPDATE_INTERVAL = 1000;

        public event EventHandler<RouteEventArgs> RoutesUpdated;

        public AutoRouteProvider(
            ISyncLocalStorageService localStorage,
            UserProvider userProvider,
            ShipsProvider shipProvider,
            ConsoleOutput console,
            CommandManager commandManager
            )
        {
            _localStorage = localStorage;
            _userProvider = userProvider;
            _shipProvider = shipProvider;
            _console = console;
            _commandManager = commandManager;

            _shipProvider.ShipsUpdated += UpdateRouteLinks;

            StartRouteRunner();
        }

        public bool RoutesLoaded()
        {
            return _routeData != null;
        }

        public bool HasRoutes()
        {
            return _routeData?.Any() ?? false;
        }

        public AutoRoute[] GetRouteData()
        {
            if (_routeData != null)
                return _routeData.Values.ToArray();
            else return Array.Empty<AutoRoute>();
        }

        public AutoRoute GetRoute(int id)
        {
            if (_routeData != null && _routeData.ContainsKey(id))
                return _routeData[id];
            else return null;
        }

        public bool TryGetRoute(int id, out AutoRoute route)
        {
            route = GetRoute(id);
            return route != null;
        }

        public void AddRoute(int id, AutoRoute route)
        {
            if(_routeData != null)
                _routeData.Add(id, route);

            SaveRouteData();

            RoutesUpdated?.Invoke(this, new RouteEventArgs
            {
                Routes = GetRouteData(),
                IsFullRefresh = false
            });
        }

        public void DeleteRoute(AutoRoute route)
        {
            var newRouteData = _routeData.Values.ToList();
            newRouteData.Remove(route);
            newRouteData.Where(r => r.Id > route.Id).ToList().ForEach(r => r.Id--);
            _routeData = newRouteData.ToDictionary(r => r.Id);

            SaveRouteData();

            RoutesUpdated?.Invoke(this, new RouteEventArgs
            {
                Routes = GetRouteData(),
                IsFullRefresh = false
            });
        }

        public AutoRoute GetShipRoute(string serverId)
        {
            return _routeData.Values.FirstOrDefault(r => r.Ships.Any(s => s.ShipId == serverId));
        }

        public void AddCommandToRoute(int routeId, int index, string command)
        {
            if (TryGetRoute(routeId, out var route))
            {
                var list = route.Commands.ToList();
                list.Where(c => c.Index >= index).ToList().ForEach(c => c.Index++);

                list.Insert(index, new RouteCommand
                {
                    Index = index,
                    Command = command,
                });

                route.Commands = list.ToArray();

                SaveRouteData();

                RoutesUpdated?.Invoke(this, new RouteEventArgs
                {
                    Routes = GetRouteData(),
                    IsFullRefresh = false
                });
            }
        }

        public void RemoveCommandFromRoute(int routeId, int index)
        {
            if (TryGetRoute(routeId, out var route))
            {
                index -= 1;
                index = Math.Max(index, 0);

                var newCommands = route.Commands.ToList();
                newCommands.Remove(newCommands.First(t => t.Index == index));
                newCommands.Where(r => r.Index > index).ToList().ForEach(r => r.Index--);
                route.Commands = newCommands.ToArray();

                SaveRouteData();

                RoutesUpdated?.Invoke(this, new RouteEventArgs
                {
                    Routes = GetRouteData(),
                    IsFullRefresh = false
                });
            }
        }

        public void AddShipToRoute(int routeId, ShipData shipData)
        {
            if (TryGetRoute(routeId, out var route))
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

                RoutesUpdated?.Invoke(this, new RouteEventArgs
                {
                    Routes = GetRouteData(),
                    IsFullRefresh = false
                });
            }
        }

        public void RemoveShipFromRoute(int routeId, RouteShip ship)
        {
            if(TryGetRoute(routeId, out var route))
            {
                var newShips = route.Ships.ToList();
                newShips.Remove(ship);
                route.Ships = newShips.ToArray();

                SaveRouteData();

                RoutesUpdated?.Invoke(this, new RouteEventArgs
                {
                    Routes = GetRouteData(),
                    IsFullRefresh = false
                });
            }
        }

        private async void UpdateRouteLinks(object sender, ShipEventArgs args)
        {
            if (args.IsFullRefresh)
                await LoadRouteData();
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
                    if (_routeData != null)
                    {
                        foreach (var route in _routeData.Values)
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

                                    var result = await _commandManager.InvokeCommand(command.Command.Replace("$s", routeShip.ShipData.Id.ToString()), true);
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

                        RoutesUpdated?.Invoke(this, new RouteEventArgs
                        {
                            Routes = GetRouteData(),
                            IsFullRefresh = false
                        });
                    }
                }
                finally
                {
                    updateLock.Release();
                }
            }
        }

        private async Task LoadRouteData()
        {
            await updateLock.WaitAsync();
            try
            {
                if (_userProvider.UserDetails != null && !_shipProvider.DataRefreshing)
                {
                    Dictionary<int, AutoRoute> newRouteData = null;
                    if (_localStorage.ContainKey("RouteData." + _userProvider.Username))
                        newRouteData = _localStorage.GetItem<Dictionary<int, AutoRoute>>("RouteData." + _userProvider.Username);
                    newRouteData ??= new Dictionary<int, AutoRoute>();

                    if (newRouteData != null)
                    {
                        foreach (var route in newRouteData)
                            foreach (var routeShip in route.Value.Ships)
                                routeShip.ShipData = _shipProvider.GetShipData(routeShip.ShipId);
                        foreach (var route in newRouteData)
                            route.Value.Ships = route.Value.Ships.Where(t => t.ShipData != null).ToArray();
                    }

                    _routeData = newRouteData;

                    SaveRouteData();
                }
                else
                {
                    _routeData = null;
                }

                RoutesUpdated?.Invoke(this, new RouteEventArgs
                {
                    Routes = GetRouteData(),
                    IsFullRefresh = true
                });
            }
            finally
            {
                updateLock.Release();
            }
        }

        private void SaveRouteData()
        {
            if (_routeData != null)
                _localStorage.SetItem("RouteData." + _userProvider.Username, _routeData);
        }
    }

    public class RouteEventArgs 
    { 
        public AutoRoute[] Routes { get; set; }
        public bool IsFullRefresh { get; set; }
    }
}
