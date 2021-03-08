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
    public class ShipsProvider
    {
        private readonly ISyncLocalStorageService _localStorage;
        private readonly SpaceTradersUserInfo _userInfo;
        private readonly StateEvents _stateEvents;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        private Dictionary<string, ShipData> _shipData;
        
        public bool DataRefreshing { get; private set; } = false;
        public DateTimeOffset LastUpdate { get; private set; } = DateTimeOffset.UtcNow;

        private const int FLIGHT_PLAN_UPDATE_INTERVAL = 100;

        public ShipsProvider(
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

            _ = RefreshShipData();

            stateEvents.StateChange += async (source, type) =>
            {
                if (type == "userChecked" || type == "userLogout" || type == "shipPurchased")
                    await RefreshShipData();
            };

            StartFlightPlanUpdater();

            commandHandler.RegisterAsyncCommand("SHIP", HandleShipCommandAsync);
        }

        public bool HasShips()
        {
            return _shipData?.Any() ?? false;
        }

        public ShipData[] GetShipData()
        {
           return _shipData.Keys.Select(t => GetShipData(t)).ToArray();
        }

        public ShipData GetShipData(string id)
        {
            if (_shipData.ContainsKey(id))
                return new ShipData
                {
                    Id = _shipData[id].Id,
                    DisplayName = _shipData[id].DisplayName,
                    ServerId = _shipData[id].ServerId,
                    LastFlightPlan = _shipData[id].LastFlightPlan,
                    FlightEnded = _shipData[id].FlightEnded,
                    TimeElapsed = _shipData[id].TimeElapsed,
                    Ship = _shipData[id].Ship
                };
            else
                return null;
        }

        public ShipData GetShipDataByLocalId(string id)
        {
            var data = GetReferencedShipData(id);
            if (data != null)
                return new ShipData
                {
                    Id = data.Id,
                    DisplayName = data.DisplayName,
                    ServerId = data.ServerId,
                    LastFlightPlan = data.LastFlightPlan,
                    FlightEnded = data.FlightEnded,
                    TimeElapsed = data.TimeElapsed,
                    Ship = data.Ship
                };
            else
                return null;
        }

        private ShipData GetReferencedShipData(string id)
        {
            var data = _shipData.Values.FirstOrDefault(t => t.Id.ToString() == id);
            data ??= _shipData.Values.FirstOrDefault(t => t.DisplayName.ToLower() == id.ToLower());
            return data;
        }

        public void UpdateShipCargo(string id, Cargo[] cargo)
        {
            if (_shipData.ContainsKey(id))
            {
                _shipData[id].Ship.Cargo = cargo;
                _shipData[id].Ship.SpaceAvailable = _shipData[id].Ship.MaxCargo - cargo.Sum(t => t.Quantity);
                SaveShipData();
            }
        }

        private void StartFlightPlanUpdater()
        {
            var timer = new Timer(FLIGHT_PLAN_UPDATE_INTERVAL);
            timer.Elapsed += UpdateFlightPlans;
            timer.Enabled = true;
        }

        private async void UpdateFlightPlans(object sender, ElapsedEventArgs args)
        {
            var flightsEnded = false;
            if(_shipData != null)
            {
                foreach (var ship in _shipData.Values)
                    if (string.IsNullOrWhiteSpace(ship.Ship.Location))
                    {
                        if (ship.LastFlightPlan != null && !ship.FlightEnded)
                        {
                            var secondsRemaining = (int)Math.Ceiling(ship.LastFlightPlan.ArrivesAt.Subtract(DateTimeOffset.UtcNow).TotalSeconds);
                            var secondsPassed = ship.LastFlightPlan.TimeRemainingInSeconds - secondsRemaining;
                            ship.LastFlightPlan.TimeRemainingInSeconds -= secondsPassed;
                            ship.TimeElapsed += secondsPassed;

                            if (ship.LastFlightPlan.TimeRemainingInSeconds < 0)
                            {
                                flightsEnded = true;
                                ship.FlightEnded = true;
                            }
                        }
                    }

                _stateEvents.TriggerUpdate(this, "flightsUpdated");
                SaveShipData();

                if (flightsEnded && !DataRefreshing)
                {
                    Console.Out.WriteLine("Flight Ended");
                    _stateEvents.TriggerUpdate(this, "flightEnded");
                    await RefreshShipData();
                }
            }
        }

        private async Task HandleShipCommandAsync(string[] args) 
        {
            if(_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return;
            }
            
            if(args.Length == 0)
            {
                _console.WriteLine("Invalid arguments. (See SHIP help)");
            }
            else if(args[0] == "?" || args[0].ToLower() == "help")
            {
                _console.WriteLine("SHIP: Provides functions for managing ships.");
                _console.WriteLine("Subcommands");
                _console.WriteLine("cargo: Displays the cargo of ship - SHIP cargo <Ship Id>");
                _console.WriteLine("fly: Enacts a flightplan for a ship - SHIP fly <Ship Id> <Location Symbol>");
                _console.WriteLine("rename: Renames a ship - SHIP rename <Ship Id> <New Name>");
                _console.WriteLine("info: Prints the specifications of ship - SHIP info <Ship Id>");
            }
            else if(args[0].ToLower() == "cargo")
            {
                if (args.Length != 2)
                    _console.WriteLine("Invalid arguments. (See SHIP help)");
                else
                {
                    var shipData = GetReferencedShipData(args[1]);
                    if (shipData != null)
                    {
                        _console.WriteLine("Displaying cargo for " + shipData.DisplayName + ".");
                        _navManager.NavigateTo(_navManager.BaseUri + "ships/cargo/" + shipData.ServerId);
                    }
                    else
                        _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                }
            }
            else if (args[0].ToLower() == "fly")
            {
                if (args.Length != 3)
                    _console.WriteLine("Invalid arguments. (See SHIP help)");
                else
                {
                    var shipData = GetReferencedShipData(args[1]);
                    if (shipData != null)
                    {
                        if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                        {
                            using var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/flight-plans", new FlightRequest
                            {
                                ShipId = shipData.ServerId,
                                Destination = args[2].ToUpper()
                            });

                            if (httpResult.StatusCode == HttpStatusCode.Created)
                            {
                                var flightResult = await httpResult.Content.ReadFromJsonAsync<FlightResponse>(_serializerOptions);

                                shipData.LastFlightPlan = flightResult.FlightPlan;
                                shipData.FlightEnded = false;
                                shipData.TimeElapsed = 0;

                                shipData.Ship.Location = null;

                                var fuel = shipData.Ship.Cargo.FirstOrDefault(t => t.Good == "FUEL");
                                if (fuel != null)
                                    if (flightResult.FlightPlan.FuelConsumed >= fuel.Quantity)
                                    {
                                        var cargoList = shipData.Ship.Cargo.ToList();
                                        cargoList.Remove(fuel);
                                        shipData.Ship.Cargo = cargoList.ToArray();
                                    }
                                    else
                                    {
                                        fuel.Quantity -= flightResult.FlightPlan.FuelConsumed;
                                        fuel.TotalVolume -= flightResult.FlightPlan.FuelConsumed;
                                    }

                                SaveShipData();

                                _stateEvents.TriggerUpdate(this, "flightStarted");
                                _console.WriteLine("Flight started successfully. Destination: " + args[2].ToUpper() + ".");
                                _navManager.NavigateTo(_navManager.BaseUri + "map/" + args[2].ToUpper().Split("-")[0]);
                            }
                            else
                            {
                                var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                                if(error.Error.Message.StartsWith("Destination does not exist."))
                                    _console.WriteLine("Destination does not exist. Please check destination and try again.");
                                else
                                    _console.WriteLine(error.Error.Message);
                            }
                        }
                        else
                            _console.WriteLine("Ship is already in transit on an existing flight plan.");
                    }
                    else
                        _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                }
            }
            else if (args[0].ToLower() == "rename")
            {
                if (args.Length != 3)
                    _console.WriteLine("Invalid arguments. (See SHIP help)");
                else
                {
                    var shipData = GetReferencedShipData(args[1]);
                    if (shipData != null)
                    {
                        shipData.DisplayName = args[2];
                        SaveShipData();

                        _console.WriteLine("Ship " + args[1] + " renamed to " + args[2] + ".");
                        _stateEvents.TriggerUpdate(this, "shipRenamed");
                    }
                    else
                        _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                }
            }
            else if (args[0].ToLower() == "info")
            {
                if (args.Length != 2)
                    _console.WriteLine("Invalid arguments. (See SHIP help)");
                else
                {
                    var shipData = GetReferencedShipData(args[1]);
                    if (shipData != null)
                    {
                        _console.WriteLine("Displaying info for " + shipData.DisplayName + ".");
                        await _console.WriteLine("Type: " + shipData.Ship.Type, 500);
                        await _console.WriteLine("Class: " + shipData.Ship.Class, 100);
                        await _console.WriteLine("Manufacturer: " + shipData.Ship.Manufacturer, 100);
                        await _console.WriteLine("Cargo Capacity: " + shipData.Ship.MaxCargo, 100);
                        await _console.WriteLine("Speed: " + shipData.Ship.Speed, 100);
                        await _console.WriteLine("Plating: " + shipData.Ship.Plating, 100);
                        await _console.WriteLine("Weapons: " + shipData.Ship.Weapons, 100);
                    }
                    else
                        _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                }
            }
            else
            {
                _console.WriteLine("Invalid arguments. (See SHIP help)");
            }
        }

        private async Task RefreshShipData()
        {
            DataRefreshing = true;

            if (_userInfo.UserDetails != null)
            {
                var shipResponse = await _http.GetFromJsonAsync<ShipsResponse>("/users/" + _userInfo.UserDetails.Username + "/ships", _serializerOptions);
                var ships = shipResponse?.Ships;

                if (_localStorage.ContainKey("ShipData." + _userInfo.Username))
                    _shipData = _localStorage.GetItem<Dictionary<string, ShipData>>("ShipData." + _userInfo.Username);
                _shipData ??= new Dictionary<string, ShipData>();
                
                foreach (var data in _shipData)
                    if (data.Value.LastFlightPlan != null)
                        data.Value.LastFlightPlan.TimeRemainingInSeconds = (int)Math.Ceiling(data.Value.LastFlightPlan.ArrivesAt.Subtract(DateTimeOffset.UtcNow).TotalSeconds);

                if (_shipData.Count > 0)
                {
                    if (ships == null)
                        _shipData.Clear();
                    else
                    {
                        var shipsToRemove = _shipData.Keys.Where(t => !ships.Any(s => t == s.Id)).ToArray();
                        foreach (var key in shipsToRemove)
                            _shipData.Remove(key);
                    }
                }

                if (ships != null)
                {
                    var shipsToAdd = ships.Where(t => !_shipData.ContainsKey(t.Id)).ToArray();
                    var currentShips = _shipData.Count;
                    for (var x = 0; x < shipsToAdd.Length; x++)
                        _shipData.Add(shipsToAdd[x].Id, new ShipData
                        {
                            Id = x + 1 + currentShips,
                            DisplayName = shipsToAdd[x].Id,
                            ServerId = shipsToAdd[x].Id,
                        });
                }

                foreach(var ship in _shipData)
                {
                    ship.Value.Ship = ships.First(t => t.Id == ship.Key);
                    var data = ship.Value;
                    ship.Value.FlightEnded = ship.Value.Ship.Location != null;
                }

                SaveShipData();
            }
            else
            {
                _shipData = null;
            }

            DataRefreshing = false;
            LastUpdate = DateTimeOffset.UtcNow;

            _stateEvents.TriggerUpdate(this, "shipsRefreshed");
        }

        private void SaveShipData()
        {
            if(_shipData != null)
                _localStorage.SetItem("ShipData." + _userInfo.Username, _shipData);
        }
    }
}
