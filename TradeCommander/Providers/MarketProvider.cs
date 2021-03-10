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

namespace TradeCommander.Providers
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

        private async Task<CommandResult> HandleMarketCommandAsync(string[] args, bool background)
        {
            if (_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return CommandResult.FAILURE;
            }

            if (args.Length > 0)
            {
                if (!background && (args[0] == "?" || args[0].ToLower() == "help"))
                {
                    _console.WriteLine("MARKET: Provides functions for interacting with the marketplace.");
                    _console.WriteLine("Subcommands");
                    _console.WriteLine("list: Displays the market data for a given location - MARKET list <Location Symbol/Ship Id>");
                    _console.WriteLine("buy: Purchase cargo from a market for the given ship - MARKET <Ship Id> buy <Good> <Quantity>");
                    _console.WriteLine("sell: Sell cargo to a market from the given ship - MARKET <Ship Id> sell <Good> <Quantity>");
                    _console.WriteLine("  Buy and sell quantities support the following formats:");
                    _console.WriteLine("  [number]: A flat number amount to buy or sell.");
                    _console.WriteLine("  max: Purchase or sell the maximum quantity possible.");
                    _console.WriteLine("  m[number]: Buy up to an amount or sell down to an amount.");
                    _console.WriteLine("  [number]%: Buy a percent of the available ship space or sell a percent of the amount in cargo.");
                    return CommandResult.SUCCESS;
                }
                else if (!background && args[0].ToLower() == "list" && args.Length == 2)
                {
                    var symbol = args[1].ToUpper();

                    var shipData = _shipInfo.GetShipDataByLocalId(args[1]);
                    if (shipData != null)
                    {
                        if (string.IsNullOrWhiteSpace(shipData.Ship.Location))
                        {
                            _console.WriteLine("Ship is not currently docked.");
                            return CommandResult.FAILURE;
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
                        return CommandResult.SUCCESS;
                    }
                    else
                        _console.WriteLine("Market data unavailable for " + symbol + ".");

                    return CommandResult.FAILURE;
                }
                else if (args.Length == 4 && args[1].ToLower() == "buy")
                {
                    var shipData = _shipInfo.GetShipDataByLocalId(args[0]);
                    if (shipData != null)
                    {
                        if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                        {
                            var quantityType = GetQuantityType(args[3], out int quantity);
                            if (quantityType != QuantityType.INVALID)
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
                                        if(good.QuantityAvailable > 0)
                                        {
                                            var spaceLeft = shipData.Ship.MaxCargo - shipData.Ship.Cargo.Sum(t => t.TotalVolume);

                                            if (quantityType == QuantityType.TO_AMOUNT)
                                            {
                                                quantity -= shipData.Ship.Cargo.Where(t => t.Good == good.Symbol).Sum(t => t.Quantity);
                                                if (quantity < 1)
                                                {
                                                    if(!background)
                                                        _console.WriteLine("Cargo already at or over given buy limit.");
                                                    return CommandResult.SUCCESS;
                                                }
                                            }
                                            else if(quantityType == QuantityType.PERCENT)
                                                quantity = (int)(shipData.Ship.MaxCargo * (quantity / 100d)) / good.VolumePerUnit;
                                            else if(quantityType == QuantityType.MAX)
                                                quantity = good.QuantityAvailable;

                                            var shipSpaceExceeded = quantity * good.VolumePerUnit > spaceLeft;
                                            if (shipSpaceExceeded && good.VolumePerUnit > 0)
                                                quantity = spaceLeft / good.VolumePerUnit;

                                            var quantityAdjusted = quantity > good.QuantityAvailable;
                                            if (quantityAdjusted)
                                                quantity = good.QuantityAvailable;
                                            
                                            if (quantityType == QuantityType.MAX && quantity * good.PricePerUnit > _userInfo.UserDetails.Credits)
                                                quantity = _userInfo.UserDetails.Credits / good.PricePerUnit;

                                            if (quantity > 0)
                                            {
                                                if (quantity * good.PricePerUnit <= _userInfo.UserDetails.Credits)
                                                {
                                                    if (!background)
                                                    {
                                                        if (quantityType == QuantityType.MAX)
                                                            _console.WriteLine("Purchasing maximum. Purchase quantity: " + quantity + ".");
                                                        else if (quantityAdjusted)
                                                            _console.WriteLine("Insufficient quantity available for purchase. Purchasing maximum.");
                                                        else if (shipSpaceExceeded)
                                                            _console.WriteLine("Insufficient cargo space available for purchase. Purchasing maximum.");
                                                    }

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
                                                        if (!background)
                                                        {
                                                            _navManager.NavigateTo(_navManager.BaseUri + "ships/cargo/" + shipData.ServerId);
                                                            _console.WriteLine(quantity + " units of cargo purchased successfully. Total cost: " + purchaseResult.Order.Sum(t => t.Total) + " credits.");
                                                        }

                                                        return CommandResult.SUCCESS;
                                                    }
                                                    else
                                                    {
                                                        var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                                                        _console.WriteLine(error.Error.Message);
                                                        if (background)
                                                            _console.WriteLine("Unhandled error occurred during route purchase. Attempting to continue.");
                                                    }
                                                }
                                                else
                                                    if (!background)
                                                        _console.WriteLine("Insufficient credits available for purchase.");
                                                    else
                                                        _console.WriteLine("Insufficient credits available for route purchase. Attempting to continue.");
                                            }
                                            else
                                                if (!background)
                                                    _console.WriteLine("Insufficient cargo space available for any purchase of this good.");
                                                else
                                                    _console.WriteLine("Route ship has insufficient cargo space. Attempting to continue.");
                                        }
                                        else
                                            if (!background)
                                                _console.WriteLine("The good specified is not in stock at this ships market.");
                                            else
                                                _console.WriteLine("Market in route is out of stock. Attempting to continue.");

                                        if (background)
                                            return CommandResult.SUCCESS;
                                    }
                                    else
                                        _console.WriteLine("The good specified could not be found at this ships market.");
                                }
                                else
                                    _console.WriteLine("An unknown error occurred while fetching market data. Please try again.");
                            }
                            else
                                _console.WriteLine("Invalid quantity provided. Must be at least 1.");
                        }
                        else
                            _console.WriteLine("Ship is not currently docked.");
                    }
                    else
                        _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");

                    return CommandResult.FAILURE;
                }
                else if (args.Length == 4 && args[1].ToLower() == "sell")
                {
                    var shipData = _shipInfo.GetShipDataByLocalId(args[0]);
                    if (shipData != null)
                    {
                        if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                        {
                            var quantityType = GetQuantityType(args[3], out int quantity);
                            if (quantityType != QuantityType.INVALID)
                            {
                                var good = shipData.Ship.Cargo.FirstOrDefault(t => t.Good == args[2].ToUpper());

                                if(good != null)
                                {
                                    if (quantityType == QuantityType.TO_AMOUNT)
                                    {
                                        quantity = good.Quantity - quantity;
                                        if (quantity < 1)
                                        {
                                            if(!background)
                                                _console.WriteLine("Cargo already at or over given sell limit.");
                                            return CommandResult.SUCCESS;
                                        }
                                    }
                                    else if (quantityType == QuantityType.PERCENT)
                                        quantity = (int)(good.Quantity * (quantity / 100d));

                                    var quantityExceeded = quantityType == QuantityType.MAX || quantity > good.Quantity;
                                    if (quantityExceeded)
                                        quantity = good.Quantity;

                                    if (!background)
                                    {
                                        if (quantityType != QuantityType.MAX && quantityExceeded)
                                            _console.WriteLine("Insufficient quantity available for sale. Selling maximum.");
                                        else if (quantityType == QuantityType.MAX)
                                            _console.WriteLine("Selling maximum. Sell quantity: " + quantity + ".");
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

                                        if (!background)
                                        {
                                            _navManager.NavigateTo(_navManager.BaseUri + "ships/cargo/" + shipData.ServerId);
                                            _console.WriteLine(quantity + " units of cargo sold successfully. Total made: " + saleResult.Order.Sum(t => t.Total) + " credits.");
                                        }

                                        return CommandResult.SUCCESS;
                                    }
                                    else
                                    {
                                        var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                                        _console.WriteLine(error.Error.Message);

                                        if (background)
                                            _console.WriteLine("Unhandled error occurred during route purchase. Attempting to continue.");
                                    }
                                }
                                else
                                    if (!background)
                                        _console.WriteLine("The good specified could not be found in the ships cargo.");
                                    else
                                        _console.WriteLine("Route ship did not have good to sell. Attempting to continue.");

                                if (background)
                                    return CommandResult.SUCCESS;
                            }
                            else
                                _console.WriteLine("Invalid quantity provided. Must be at least 1.");
                        }
                        else
                            _console.WriteLine("Ship is not currently docked.");
                    }
                    else
                        _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");

                    return CommandResult.FAILURE;
                }
            }
            
            return CommandResult.INVALID;
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

        private static QuantityType GetQuantityType(string quantityString, out int quantity)
        {
            quantityString = quantityString.ToLower();

            var quantityType = QuantityType.FLAT;
            if (quantityString == "max")
                quantityType = QuantityType.MAX;
            else if (quantityString.StartsWith("m"))
            {
                quantityType = QuantityType.TO_AMOUNT;
                quantityString = quantityString.Remove(0, 1);
            }
            else if (quantityString.EndsWith("%"))
            {
                quantityType = QuantityType.PERCENT;
                quantityString = quantityString.Remove(quantityString.Length -1);
            }

            quantity = 0;
            if (quantityType != QuantityType.MAX)
                if (!int.TryParse(quantityString, out quantity))
                    quantityType = QuantityType.INVALID;
                else if(quantity < 1)
                    quantityType = QuantityType.INVALID;

            return quantityType;
        }

        private enum QuantityType 
        {
            FLAT, MAX, TO_AMOUNT, PERCENT, INVALID
        }
    }
}
