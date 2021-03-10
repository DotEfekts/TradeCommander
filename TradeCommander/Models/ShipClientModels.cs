using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class ShipData
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public string ServerId { get; set; }
        public FlightPlan LastFlightPlan { get; set; }
        public int TimeElapsed { get; set; } = 0;
        public bool FlightEnded { get; set; } = true;
        [JsonIgnore]
        public Ship Ship { get; set; }
    }
}
