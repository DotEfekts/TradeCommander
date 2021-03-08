using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SpaceTraders_Client.Models
{
    public class AutoRoute
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public RouteCommand[] Commands { get; set; }
        public string[] ShipsIds { get; set; }
        [JsonIgnore]
        public Ship[] Ships { get; set; }
    }

    public class RouteCommand
    {
        public int Index { get; set; }
        public string Command { get; set; }
        public string Location { get; set; }
    }
}
