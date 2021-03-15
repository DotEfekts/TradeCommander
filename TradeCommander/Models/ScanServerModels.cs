using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TradeCommander.Models
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
        public Location Location { get; set; }
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
        public string Anomaly { get; set; }
    }

    public class SystemFlightPlansResponse
    {
        public SystemFlightPlan[] FlightPlans { get; set; }
    }

    public class SystemFlightPlan
    {
        public string Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ArrivesAt { get; set; }
        public string To { get; set; }
        public string From { get; set; }
        public string Username { get; set; }
        public string ShipType { get; set; }
    }
}
