using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class MarketResponse
    {
        public MarketLocation Location { get; set; }
    }

    public class MarketLocation
    {
        public string Symbol { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public MarketGood[] Marketplace { get; set; }
    }

    public class MarketGood
    {
        public string Symbol { get; set; }
        public int PricePerUnit { get; set; }
        public int QuantityAvailable { get; set; }
        public int VolumePerUnit { get; set; }
        public int Spread { get; set; }
    }

    public class TransactionRequest
    {
        public string ShipId { get; set; }
        public string Good { get; set; }
        public int Quantity { get; set; }
    }

    public class TransactionResult
    { 
        public int Credits { get; set; }
        public Order Order { get; set; }
        public Ship Ship { get; set; }

    }

    public class Order 
    { 
        public string Good { get; set; }
        public int Quantity { get; set; }
        public int PricePerUnit { get; set; }
        public int Total { get; set; }
    }
}
