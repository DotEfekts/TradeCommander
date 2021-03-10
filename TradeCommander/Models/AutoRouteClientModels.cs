using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class AutoRoute
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public RouteCommand[] Commands { get; set; }
        public RouteShip[] Ships { get; set; }
    }

    public class RouteCommand
    {
        public int Index { get; set; }
        public string Command { get; set; }
    }

    public class RouteShip
    {
        public int LastCommand { get; set; }
        public string ShipId { get; set; }
        [JsonIgnore]
        public ShipData ShipData { get; set; }
    }
}
