using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SpaceTraders_Client.Models
{
    public class Market
    {
        public string Symbol { get; set; }
        public DateTimeOffset RetrievedAt { get; set; }
        public MarketGood[] Marketplace { get; set; }
    }
}
