using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SpaceTraders_Client.Models
{
    public class GlobalResponse
    {
        public SpaceSystem[] Systems { get; set; }
    }

    public class SystemResponse
    {
        public Location[] Locations { get; set; }
    }

    public class LocationResponse
    {
        public Location Planet { get; set; }
    }

    public class SpaceSystem
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public Location[] Locations { get; set; }
    }

    public class Location
    {
        public string Symbol { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public double? AnsibleProgress { get; set; }
    }
}
