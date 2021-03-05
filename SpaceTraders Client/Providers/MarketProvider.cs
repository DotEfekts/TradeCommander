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
    public class MarketProvider
    {
        private readonly ISyncLocalStorageService _localStorage;
        private readonly SpaceTradersUserInfo _userInfo;
        private readonly ShipsProvider _shipInfo;
        private readonly StateEvents _stateEvents;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        private Dictionary<string, Market> _marketData;

        public MarketProvider(
            ISyncLocalStorageService localStorage,
            SpaceTradersUserInfo userInfo,
            ShipsProvider shipInfo,
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
            _shipInfo = shipInfo;
            _stateEvents = stateEvents;
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;

            LoadMarketData();

            stateEvents.StateChange += async (source, type) =>
            {
                if (type == "userLogin" || type == "shipDocked" || type == "shipPurchased")
                    await RefreshMarketData();
            };

            commandHandler.RegisterAsyncCommand("MARKET", HandleMarketCommandAsync);
        }

        public Market GetMarket(string symbol)
        {
            return _marketData.ContainsKey(symbol) ? _marketData[symbol] : null;
        }

        private void LoadMarketData()
        {
            if (_localStorage.ContainKey("MarketData"))
                _marketData = _localStorage.GetItem<Dictionary<string, Market>>("MarketData");

            _marketData ??= new Dictionary<string, Market>();
        }

        private async Task HandleMarketCommandAsync(string[] args)
        {
            if (_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                _console.WriteLine("Invalid arguments. (See MARKET help)");
            }
            else if (args[0] == "?" || args[0].ToLower() == "help")
            {
                _console.WriteLine("MARKET: Provides functions for interacting with the marketplace.");
                _console.WriteLine("Subcommands");
                _console.WriteLine("list: Displays the market data for a given location - MARKET list <Location Symbol/Ship Id>");
                _console.WriteLine("buy: Purchase cargo from a market for the given ship - MARKET buy <Ship Id> <Good> <Quantity>");
                _console.WriteLine("sell: Sell cargo to a market from the given ship - MARKET sell <Ship Id> <Good> <Quantity>");
            }
            else if (args[0].ToLower() == "list")
            {
                if (args.Length != 2)
                    _console.WriteLine("Invalid arguments. (See MARKET help)");
                else
                {
                    var symbol = args[1].ToUpper();

                    var shipData = _shipInfo.GetShipDataByLocalId(args[1]);
                    if (shipData != null)
                    {
                        if (string.IsNullOrWhiteSpace(shipData.Ship.Location))
                        {
                            _console.WriteLine("Ship is not currently docked.");
                            return;
                        }

                        symbol = shipData.Ship.Location;
                    }

                    var canGetLive = _shipInfo.GetShipData().Any(t => t.Ship.Location == symbol);
                    if (canGetLive)
                    {
                        await RefreshMarketData(symbol);
                        _stateEvents.TriggerUpdate(this, "marketUpdated");
                        SaveMarketData();

                        _console.WriteLine("Retrieved updated market data for " + symbol + ".");
                    }

                    if (_marketData.ContainsKey(symbol))
                    {
                        _console.WriteLine("Displaying market data for " + symbol + ".");
                        _navManager.NavigateTo(_navManager.BaseUri + "markets/" + symbol);
                    }
                    else
                        _console.WriteLine("Market data unavailable for " + symbol + ".");
                }
            }
            else if (args[0].ToLower() == "buy")
            {

                if (args.Length != 4)
                    _console.WriteLine("Invalid arguments. (See MARKET help)");
                else
                {
                    var shipData = _shipInfo.GetShipDataByLocalId(args[1]);
                    if (shipData != null)
                    {
                        if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                        {
                            if (int.TryParse(args[3], out int quantity))
                            {
                                var response = await RefreshMarketData(shipData.Ship.Location);
                                _stateEvents.TriggerUpdate(this, "marketUpdated");
                                SaveMarketData();

                                if (response != null)
                                {
                                    var market = response.Planet.Marketplace;
                                    var good = market.FirstOrDefault(t => t.Symbol == args[2].ToUpper());
                                    
                                    if (good != null)
                                    {
                                        if(good.Available > 0)
                                        {
                                            var shipSpaceExceeded = quantity > shipData.Ship.SpaceAvailable;
                                            if (shipSpaceExceeded)
                                                quantity = shipData.Ship.SpaceAvailable;

                                            var quantityAdjusted = quantity > good.Available;
                                            if (quantityAdjusted)
                                                quantity = good.Available;

                                            if (quantity * good.PricePerUnit <= _userInfo.UserDetails.Credits)
                                            {
                                                if (quantityAdjusted)
                                                    _console.WriteLine("Insufficient quantity available for purchase. Purchasing maximum.");
                                                else if (shipSpaceExceeded)
                                                    _console.WriteLine("Insufficient cargo space available for purchase. Purchasing maximum.");

                                                using var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/purchase-orders", new TransactionRequest
                                                {
                                                    ShipId = shipData.ServerId,
                                                    Good = good.Symbol,
                                                    Quantity = quantity
                                                });

                                                if (httpResult.StatusCode == HttpStatusCode.Created)
                                                {
                                                    var purchaseResult = await httpResult.Content.ReadFromJsonAsync<TransactionResult>(_serializerOptions);

                                                    _userInfo.UserDetails.Credits = purchaseResult.Credits;

                                                    _shipInfo.UpdateShipCargo(purchaseResult.Ship.Id, purchaseResult.Ship.Cargo);
                                                    _stateEvents.TriggerUpdate(this, "cargoPurchased");
                                                    _navManager.NavigateTo(_navManager.BaseUri + "ships/cargo/" + shipData.ServerId);
                                                    _console.WriteLine("Cargo purchased successfully. Total cost: " + purchaseResult.Order.Sum(t => t.Total) + " credits.");
                                                }
                                                else
                                                {
                                                    var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                                                    _console.WriteLine(error.Error.Message);
                                                }
                                            }
                                            else
                                                _console.WriteLine("Insufficient credits available for purchase.");
                                        }
                                        else
                                            _console.WriteLine("The good specified is not in stock at this ships market.");
                                    }
                                    else
                                        _console.WriteLine("The good specified could not be found at this ships market.");
                                }
                                else
                                    _console.WriteLine("An unknown error occurred while fetching market data. Please try again.");
                            }
                            else
                                _console.WriteLine("Invalid quantity provided.");
                        }
                        else
                            _console.WriteLine("Ship is not currently docked.");
                    }
                    else
                        _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                }
            }
            else if (args[0].ToLower() == "sell")
            {

                if (args.Length != 4)
                    _console.WriteLine("Invalid arguments. (See MARKET help)");
                else
                {
                    var shipData = _shipInfo.GetShipDataByLocalId(args[1]);
                    if (shipData != null)
                    {
                        if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                        {
                            if (int.TryParse(args[3], out int quantity))
                            {
                                var good = shipData.Ship.Cargo.FirstOrDefault(t => t.Good == args[2].ToUpper());

                                if(good != null)
                                {
                                    var quantityExceeded = quantity > good.Quantity;
                                    if (quantityExceeded)
                                    {
                                        quantity = good.Quantity;
                                        _console.WriteLine("Insufficient quantity available for sale. Selling maximum.");
                                    }

                                    var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/sell-orders", new TransactionRequest
                                    {
                                        ShipId = shipData.ServerId,
                                        Good = good.Good,
                                        Quantity = quantity
                                    });

                                    if (httpResult.StatusCode == HttpStatusCode.Created)
                                    {
                                        var saleResult = await httpResult.Content.ReadFromJsonAsync<TransactionResult>(_serializerOptions);
                                        _userInfo.UserDetails.Credits = saleResult.Credits;

                                        _shipInfo.UpdateShipCargo(saleResult.Ship.Id, saleResult.Ship.Cargo);
                                        _stateEvents.TriggerUpdate(this, "cargoSold");
                                        _navManager.NavigateTo(_navManager.BaseUri + "ships/cargo/" + shipData.ServerId);
                                        _console.WriteLine("Cargo sold successfully. Total made: " + saleResult.Order.Sum(t => t.Total) + " credits.");
                                    }
                                    else
                                    {
                                        var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                                        _console.WriteLine(error.Error.Message);
                                    }
                                }
                                else
                                    _console.WriteLine("The good specified could not be found in the ships cargo.");
                            }
                            else
                                _console.WriteLine("Invalid quantity provided.");
                        }
                        else
                            _console.WriteLine("Ship is not currently docked.");
                    }
                    else
                        _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                }
            }
            else
            {
                _console.WriteLine("Invalid arguments. (See MARKET help)");
            }
        }

        private async Task RefreshMarketData()
        {
            if (_userInfo.UserDetails != null && _shipInfo.HasShips())
            {
                var locations = _shipInfo.GetShipData().Select(t => t.Ship.Location).Distinct().Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
                foreach (var location in locations)
                    await RefreshMarketData(location);
                
                _stateEvents.TriggerUpdate(this, "marketUpdated");
                SaveMarketData();
            }
        }

        private async Task<MarketResponse> RefreshMarketData(string symbol)
        {
            var response = await _http.GetFromJsonAsync<MarketResponse>("/game/locations/" + symbol + "/marketplace", _serializerOptions);
            if (response != null)
            {
                _marketData[response.Planet.Symbol] = new Market
                {
                    Symbol = response.Planet.Symbol,
                    RetrievedAt = DateTimeOffset.UtcNow,
                    Marketplace = response.Planet.Marketplace
                };
            }

            return response;
        }

        private void SaveMarketData()
        {
            _localStorage.SetItem("MarketData", _marketData);
        }
    }
}
