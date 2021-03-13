using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class ClientSystemFlightPlan : SystemFlightPlan
    {
        public int TimeElapsed { get; set; }
        public int TimeRemaining { get; set; }
    }
}
