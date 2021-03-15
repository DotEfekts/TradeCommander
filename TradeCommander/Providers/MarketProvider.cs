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
        private readonly UserProvider _userProvider;
        private readonly ShipsProvider _shipProvider;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly Dictionary<string, Market> _marketData;

        public event EventHandler<MarketEventArgs> MarketsUpdated;

        public MarketProvider(
            ISyncLocalStorageService localStorage,
            UserProvider userProvider,
            ShipsProvider shipProvider,
            HttpClient http,
            JsonSerializerOptions serializerOptions
            )
        {
            _localStorage = localStorage;
            _userProvider = userProvider;
            _shipProvider = shipProvider;
            _http = http;
            _serializerOptions = serializerOptions;
            _marketData = new Dictionary<string, Market>();

            _shipProvider.ShipsUpdated += UpdateMarkets;

            LoadMarketData();
        }

        public bool HasMarket(string symbol)
        {
            return _marketData.ContainsKey(symbol.ToUpper());
        }

        public bool TryGetMarket(string symbol, out Market market)
        {
            market = GetMarket(symbol);
            return market != null;
        }

        public Market GetMarket(string symbol)
        {
            return HasMarket(symbol) ? _marketData[symbol.ToUpper()] : null;
        }

        private void LoadMarketData()
        {
            if (_localStorage.ContainKey("MarketData"))
            {
                var loadedData = _localStorage.GetItem<Dictionary<string, Market>>("MarketData");
                foreach (var data in loadedData)
                    if(!_marketData.ContainsKey(data.Key))
                        _marketData[data.Key] = data.Value;
            }
        }

        private async void UpdateMarkets(object sender, ShipEventArgs args)
        {
            if (args.IsFullRefresh)
                await RefreshMarketData();
        }

        private async Task RefreshMarketData()
        {
            if (_userProvider.UserDetails != null && _shipProvider.HasShips())
            {
                var locations = _shipProvider.GetShipData().Select(t => t.Ship.Location).Distinct().Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
                foreach (var location in locations)
                    await RefreshMarketData(location, true);

                SaveMarketData();

                MarketsUpdated?.Invoke(this, new MarketEventArgs
                {
                    Markets = _marketData.Values.ToArray()
                });
            }

        }

        public async Task<MarketResponse> RefreshMarketData(string symbol, bool chainedUpdate)
        {
            var response = await _http.GetFromJsonAsync<MarketResponse>("/game/locations/" + symbol.ToUpper() + "/marketplace", _serializerOptions);
            if (_marketData != null && response != null)
            {
                _marketData[response.Location.Symbol.ToUpper()] = new Market
                {
                    Symbol = response.Location.Symbol.ToUpper(),
                    RetrievedAt = DateTimeOffset.UtcNow,
                    Marketplace = response.Location.Marketplace
                };

                if (!chainedUpdate)
                {
                    SaveMarketData();

                    MarketsUpdated?.Invoke(this, new MarketEventArgs
                    {
                        Markets = _marketData.Values.ToArray()
                    });
                }
            }

            return response;
        }

        private void SaveMarketData()
        {
            _localStorage.SetItem("MarketData", _marketData);
        }
    }

    public class MarketEventArgs
    {
        public Market[] Markets { get; set; }
    }
}
