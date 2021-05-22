using Microsoft.AspNetCore.Components;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeCommander.Models;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class MarketCommandHandler : ICommandHandlerAsync
    {
        private readonly UserProvider _userInfo;
        private readonly ShipsProvider _shipProvider;
        private readonly MarketProvider _marketInfo;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;

        private const int TRADE_CAP = 300;

        public MarketCommandHandler(
            UserProvider userInfo,
            ShipsProvider shipProvider,
            MarketProvider marketInfo,
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions
            )
        {
            _userInfo = userInfo;
            _shipProvider = shipProvider;
            _marketInfo = marketInfo;
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;
        }

        public string CommandName => "MARKET";
        public bool BackgroundCanUse => true;
        public bool RequiresLogin => true;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public async Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn)
        {
            if (!background && args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
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
            else if (!background && args.Length == 2 && args[0].ToLower() == "list")
            {
                var symbol = args[1].ToUpper();
                string shipId = null;

                if (_shipProvider.TryGetShipDataByLocalId(args[1], out var shipData))
                {
                    if (string.IsNullOrWhiteSpace(shipData.Ship.Location))
                    {
                        _console.WriteLine("Ship is not currently docked.");
                        return CommandResult.FAILURE;
                    }

                    symbol = shipData.Ship.Location;
                    shipId = shipData.ServerId;
                }

                var canGetLive = _shipProvider.GetShipData().Any(t => t.Ship.Location == symbol);
                if (canGetLive)
                {
                    await _marketInfo.RefreshMarketData(symbol, false);
                    _console.WriteLine("Retrieved updated market data for " + symbol + ".");
                }

                if (_marketInfo.HasMarket(symbol))
                {
                    _console.WriteLine("Displaying market data for " + symbol + ".");
                    if(shipId != null)
                        _navManager.NavigateTo(_navManager.BaseUri + "ships/" + shipId + "/market");
                    else
                        _navManager.NavigateTo(_navManager.BaseUri + "markets/" + symbol);
                    return CommandResult.SUCCESS;
                }
                else
                    _console.WriteLine("Market data unavailable for " + symbol + ".");

                return CommandResult.FAILURE;
            }
            else if (args.Length == 4 && args[1].ToLower() == "buy")
            {
                if (_shipProvider.TryGetShipDataByLocalId(args[0], out var shipData))
                {
                    if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                    {
                        var quantityType = GetQuantityType(args[3], out int quantity);
                        if (quantityType != QuantityType.INVALID)
                        {
                            return await ProcessPurchase(shipData, quantityType, quantity, args[2], background, false);
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
                if (_shipProvider.TryGetShipDataByLocalId(args[0], out var shipData))
                {
                    if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                    {
                        var quantityType = GetQuantityType(args[3], out int quantity);
                        if (quantityType != QuantityType.INVALID)
                        {
                            var good = shipData.Ship.Cargo.FirstOrDefault(t => t.Good == args[2].ToUpper());

                            if (good != null)
                            {
                                if (quantityType == QuantityType.TO_AMOUNT)
                                {
                                    quantity = good.Quantity - quantity;
                                    if (quantity < 1)
                                    {
                                        if (!background)
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

                                var quantityRemaining = quantity;
                                var anySold = false;
                                while(quantityRemaining > 0)
                                {
                                    var quantityProcessing = Math.Min(TRADE_CAP, quantityRemaining);
                                    quantityRemaining -= quantityProcessing;

                                    var httpResult = await _http.PostAsJsonAsync("/my/sell-orders", new TransactionRequest
                                    {
                                        ShipId = shipData.ServerId,
                                        Good = good.Good,
                                        Quantity = quantityProcessing
                                    });

                                    if (httpResult.IsSuccessStatusCode)
                                    {
                                        var saleResult = await httpResult.Content.ReadFromJsonAsync<TransactionResult>(_serializerOptions);

                                        _userInfo.SetCredits(saleResult.Credits);
                                        _shipProvider.UpdateShipCargo(saleResult.Ship.Id, saleResult.Ship.Cargo);

                                        if (!background)
                                        {
                                            _console.WriteLine(quantityProcessing + " units of cargo sold successfully. Total made: " + saleResult.Order.Total + " credits.");
                                        }

                                        anySold = true;

                                        if (quantityRemaining <= 0)
                                            return CommandResult.SUCCESS;
                                    }
                                    else
                                    {
                                        var errorResponseString = await httpResult.Content.ReadAsStringAsync();
                                        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(errorResponseString, _serializerOptions);

                                        if (errorResponse.Error == null)
                                            errorResponse.Error = JsonSerializer.Deserialize<Error>(errorResponseString, _serializerOptions);

                                        if (errorResponse.Error.Message.ToLower().Contains("good is not listed in planet marketplace"))
                                            _console.WriteLine("The good specified cannot be sold at this ships market.");
                                        else
                                        {
                                            _console.WriteLine(errorResponse.Error.Message);

                                            if (background)
                                                _console.WriteLine("Unhandled error occurred during route purchase. Attempting to continue.");
                                        }

                                        return anySold ? CommandResult.SUCCESS : CommandResult.FAILURE;
                                    }
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

            return CommandResult.INVALID;
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
                quantityString = quantityString.Remove(quantityString.Length - 1);
            }

            quantity = 0;
            if (quantityType != QuantityType.MAX)
                if (!int.TryParse(quantityString, out quantity))
                    quantityType = QuantityType.INVALID;
                else if (quantity < 1)
                    quantityType = QuantityType.INVALID;

            return quantityType;
        }

        private async Task<CommandResult> ProcessPurchase(ShipData shipData, QuantityType quantityType, int quantity, string symbol, bool background, bool repeated)
        {
            var response = await _marketInfo.RefreshMarketData(shipData.Ship.Location, false);
            if (response != null)
            {
                var market = response.Marketplace;
                var good = market.FirstOrDefault(t => t.Symbol == symbol.ToUpper());

                if (good != null)
                {
                    if (good.QuantityAvailable > 0)
                    {
                        var spaceLeft = shipData.Ship.MaxCargo - shipData.Ship.Cargo.Sum(t => t.TotalVolume);

                        if (quantityType == QuantityType.TO_AMOUNT)
                        {
                            quantity -= shipData.Ship.Cargo.Where(t => t.Good == good.Symbol).Sum(t => t.Quantity);
                            if (quantity < 1)
                            {
                                if (!background)
                                    _console.WriteLine("Cargo already at or over given buy limit.");
                                return CommandResult.SUCCESS;
                            }
                        }
                        else if (quantityType == QuantityType.PERCENT)
                            quantity = (int)(shipData.Ship.MaxCargo * (quantity / 100d)) / good.VolumePerUnit;
                        else if (quantityType == QuantityType.MAX)
                            quantity = good.QuantityAvailable;

                        var shipSpaceExceeded = quantity * good.VolumePerUnit > spaceLeft;
                        if (shipSpaceExceeded && good.VolumePerUnit > 0)
                            quantity = spaceLeft / good.VolumePerUnit;

                        var quantityAdjusted = quantity > good.QuantityAvailable;
                        if (quantityAdjusted)
                            quantity = good.QuantityAvailable;

                        var buyPrice = good.PricePerUnit + good.Spread;
                        if (quantityType == QuantityType.MAX && quantity * buyPrice > _userInfo.UserDetails.Credits)
                            quantity = _userInfo.UserDetails.Credits / buyPrice;

                        if (quantity > 0 || (quantityType == QuantityType.MAX && spaceLeft >= good.VolumePerUnit))
                        {
                            if (quantityType == QuantityType.TO_AMOUNT && quantity * buyPrice > _userInfo.UserDetails.Credits)
                                quantity = _userInfo.UserDetails.Credits / buyPrice;

                            if (quantity > 0 && quantity * buyPrice <= _userInfo.UserDetails.Credits)
                            {
                                if (!background && !repeated)
                                {
                                    if (quantityType == QuantityType.MAX)
                                        _console.WriteLine("Maximum purchase requested, please standby.");
                                    else if (quantityAdjusted)
                                        _console.WriteLine("Insufficient quantity available for purchase. Purchasing maximum.");
                                    else if (shipSpaceExceeded)
                                        _console.WriteLine("Insufficient cargo space available for purchase. Purchasing maximum.");
                                }

                                var quantityProcessing = Math.Min(TRADE_CAP, quantity);

                                using var httpResult = await _http.PostAsJsonAsync("/my/purchase-orders", new TransactionRequest
                                {
                                    ShipId = shipData.ServerId,
                                    Good = good.Symbol,
                                    Quantity = quantityProcessing
                                });

                                if (httpResult.IsSuccessStatusCode)
                                {
                                    var purchaseResult = await httpResult.Content.ReadFromJsonAsync<TransactionResult>(_serializerOptions);

                                    _userInfo.SetCredits(purchaseResult.Credits);
                                    _shipProvider.UpdateShipCargo(purchaseResult.Ship.Id, purchaseResult.Ship.Cargo);

                                    if (!background)
                                    {
                                        _console.WriteLine(quantityProcessing + " units of cargo purchased successfully. Total cost: " + purchaseResult.Order.Total + " credits.");
                                    }

                                    if (quantity - quantityProcessing > 0)
                                        await ProcessPurchase(shipData, quantityType != QuantityType.MAX ? QuantityType.FLAT : QuantityType.MAX, quantity - quantityProcessing, symbol, background, true);
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

            return CommandResult.FAILURE;
        }

        private enum QuantityType
        {
            FLAT, MAX, TO_AMOUNT, PERCENT, INVALID
        }
    }
}
