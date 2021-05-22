using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class MarketResponse
    {
        public MarketGood[] Marketplace { get; set; }
    }

    public class MarketGood
    {
        public string Symbol { get; set; }
        public int PricePerUnit { get; set; }
        public int PurchasePricePerUnit { get; set; }
        public int SellPricePerUnit { get; set; }
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
