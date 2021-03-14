using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using TradeCommander.Models;

namespace TradeCommander.Providers
{
    public class ShipsProvider
    {
        private readonly ISyncLocalStorageService _localStorage;
        private readonly UserProvider _userProvider;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        private Dictionary<string, ShipData> _shipData;

        private readonly string _appBase;
        private readonly Random _rand;
        private string[] _shipNames;

        public bool DataRefreshing { get; private set; } = true;

        public event EventHandler<ShipEventArgs> FlightsUpdated;
        public event EventHandler<ShipEventArgs> ShipsUpdated;

        public DateTimeOffset LastUpdate { get; private set; } = DateTimeOffset.UtcNow;

        private const int FLIGHT_PLAN_UPDATE_INTERVAL = 1000;

        public ShipsProvider(
            ISyncLocalStorageService localStorage,
            UserProvider userProvider,
            HttpClient http,
            JsonSerializerOptions serializerOptions,
            NavigationManager navManager
            )
        {
            _localStorage = localStorage;
            _userProvider = userProvider;
            _http = http;
            _serializerOptions = serializerOptions;

            _appBase = navManager.BaseUri;
            _rand = new Random();

            _userProvider.UserUpdated += HandleUserUpdate;
            
            LoadShipNames();
            StartFlightPlanUpdater();
        }

        public bool HasShips()
        {
            return _shipData?.Any() ?? false;
        }

        public ShipData[] GetShipData()
        {
            if (_shipData != null)
                return _shipData.Values.ToArray();
            else return Array.Empty<ShipData>();
        }

        public ShipData GetShipData(string id)
        {
            TryGetShipData(id, out var shipData);
            return shipData;
        }

        public bool TryGetShipData(string id, out ShipData shipData)
        {
            if (_shipData == null || id == null)
                shipData = null;
            else
                _shipData.TryGetValue(id, out shipData);
            return shipData != null;
        }

        public ShipData GetShipDataByLocalId(string id)
        {
            TryGetShipDataByLocalId(id, out var shipData);
            return shipData;
        }

        public bool TryGetShipDataByLocalId(string id, out ShipData shipData)
        {
            if (_shipData == null || id == null)
                shipData = null;
            else
            {
                shipData = _shipData.Values.FirstOrDefault(t => t.Id.ToString() == id);
                shipData ??= _shipData.Values.FirstOrDefault(t => t.DisplayName.ToLower() == id.ToLower());
            }

            return shipData != null;
        }

        public void UpdateShipName(string id, string name)
        {
            if (TryGetShipData(id, out var shipData))
            {
                shipData.DisplayName = name;

                SaveShipData();

                ShipsUpdated?.Invoke(this, new ShipEventArgs
                {
                    ShipDetails = GetShipData(),
                    IsFullRefresh = false
                });
            }
        }

        public void UpdateShipCargo(string id, Cargo[] cargo)
        {
            if (TryGetShipData(id, out var shipData))
            {
                shipData.Ship.Cargo = cargo;
                shipData.Ship.SpaceAvailable = shipData.Ship.MaxCargo - cargo.Sum(t => t.Quantity);

                SaveShipData();

                ShipsUpdated?.Invoke(this, new ShipEventArgs
                {
                    ShipDetails = GetShipData(),
                    IsFullRefresh = false
                });
            }
        }

        public void AddFlightPlan(string id, FlightPlan flightPlan)
        {
            if (TryGetShipData(id, out var shipData))
            {
                shipData.LastFlightPlan = flightPlan;
                shipData.FlightEnded = false;
                shipData.TimeElapsed = 0;
                shipData.Ship.Location = null;

                var fuel = shipData.Ship.Cargo.FirstOrDefault(t => t.Good == "FUEL");
                if (fuel != null)
                    if (flightPlan.FuelConsumed >= fuel.Quantity)
                    {
                        var cargoList = shipData.Ship.Cargo.ToList();
                        cargoList.Remove(fuel);
                        shipData.Ship.Cargo = cargoList.ToArray();
                    }
                    else
                    {
                        fuel.Quantity -= flightPlan.FuelConsumed;
                        fuel.TotalVolume -= flightPlan.FuelConsumed;
                    }

                SaveShipData();

                FlightsUpdated?.Invoke(this, new ShipEventArgs
                {
                    ShipDetails = GetShipData(),
                    IsFullRefresh = false
                });

                ShipsUpdated?.Invoke(this, new ShipEventArgs
                {
                    ShipDetails = GetShipData(),
                    IsFullRefresh = false
                });
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
                {
                    if (ship.Ship == null)
                        Console.Error.WriteLine(ship.DisplayName + " has no attached ship.");
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

                                if(!DataRefreshing)
                                    SaveShipData();
                            }
                        }
                    }
                }

                if (flightsEnded && !DataRefreshing)
                    await RefreshShipData();

                FlightsUpdated?.Invoke(this, new ShipEventArgs
                {
                    ShipDetails = GetShipData()
                });
            }
        }

        private async void LoadShipNames()
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(_appBase + "/ship-names.json"),
                Method = HttpMethod.Get
            };

            var httpResponse = await _http.SendAsync(request);
            _shipNames = await httpResponse.Content.ReadFromJsonAsync<string[]>(_serializerOptions);

            if (HasShips())
            {
                AssignShipNames();
                SaveShipData();
            }
        }

        private void AssignShipNames()
        {
            if(_shipData != null && _shipNames != null)
                foreach(var ship in _shipData.Values)
                {
                    if(ship.DisplayName == ship.ServerId)
                    {
                        var currentNames = _shipData.Values.Select(s => s.DisplayName);
                        if (!_shipNames.All(n => currentNames.Contains(n)))
                        {
                            var randomNames = _shipNames.OrderBy(n => _rand.Next());
                            foreach(var name in randomNames)
                                if (!currentNames.Contains(name))
                                {
                                    ship.DisplayName = name;
                                    break;
                                }
                        }
                        else
                            ship.DisplayName = ship.Id.ToString();
                    }
                }
        }

        private async void HandleUserUpdate(object sender, UserEventArgs eventArgs)
        {
            await RefreshShipData();
        }

        public async Task RefreshShipData()
        {
            DataRefreshing = true;

            if (_userProvider.UserDetails != null)
            {
                Dictionary<string, ShipData> newShipData = null;
                var shipResponse = await _http.GetFromJsonAsync<ShipsResponse>("/users/" + _userProvider.UserDetails.Username + "/ships", _serializerOptions);
                var ships = shipResponse?.Ships;

                if (_localStorage.ContainKey("ShipData." + _userProvider.Username))
                    newShipData = _localStorage.GetItem<Dictionary<string, ShipData>>("ShipData." + _userProvider.Username);
                newShipData ??= new Dictionary<string, ShipData>();

                if (newShipData.Count > 0)
                {
                    if (ships == null)
                        newShipData.Clear();
                    else
                    {
                        var shipsToRemove = newShipData.Keys.Where(t => !ships.Any(s => t == s.Id)).ToArray();
                        foreach (var key in shipsToRemove)
                            newShipData.Remove(key);
                    }
                }

                if (ships != null)
                {
                    var shipsToAdd = ships.Where(t => !newShipData.ContainsKey(t.Id)).ToArray();
                    var currentShips = newShipData.Count;
                    for (var x = 0; x < shipsToAdd.Length; x++)
                        newShipData.Add(shipsToAdd[x].Id, new ShipData
                        {
                            Id = x + 1 + currentShips,
                            DisplayName = shipsToAdd[x].Id,
                            ServerId = shipsToAdd[x].Id,
                        });
                }

                var timeNow = DateTimeOffset.UtcNow;
                foreach (var ship in newShipData)
                {
                    ship.Value.Ship = ships.First(t => t.Id == ship.Key);

                    if (ship.Value.Ship.Location == null && ship.Value.LastFlightPlan?.Id != ship.Value.Ship.FlightPlanId)
                    {
                        ship.Value.LastFlightPlan = null;

                        try
                        {
                            var flightPlanResponse = await _http.GetFromJsonAsync<FlightResponse>("/users/" + _userProvider.UserDetails.Username + "/flight-plans/" + ship.Value.Ship.FlightPlanId, _serializerOptions);
                            ship.Value.LastFlightPlan = flightPlanResponse.FlightPlan;
                        }
                        catch (Exception) { }
                    }

                    if (ship.Value.LastFlightPlan != null)
                        ship.Value.LastFlightPlan.TimeRemainingInSeconds = (int)Math.Ceiling(ship.Value.LastFlightPlan.ArrivesAt.Subtract(timeNow).TotalSeconds);
                    ship.Value.FlightEnded = ship.Value.LastFlightPlan == null || ship.Value.LastFlightPlan?.TimeRemainingInSeconds < 0;
                }

                _shipData = newShipData;

                AssignShipNames();
                SaveShipData();
            }
            else
            {
                _shipData = null;
            }

            DataRefreshing = false;
            LastUpdate = DateTimeOffset.UtcNow;

            ShipsUpdated?.Invoke(this, new ShipEventArgs
            {
                ShipDetails = GetShipData(),
                IsFullRefresh = true
            });
        }

        private void SaveShipData()
        {
            if(_shipData != null)
                _localStorage.SetItem("ShipData." + _userProvider.Username, _shipData);
        }
    }

    public class ShipEventArgs
    {
        public ShipData[] ShipDetails { get; set; }
        public bool IsFullRefresh { get; set; }
    }
}
