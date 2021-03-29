using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class ShipsResponse
    {
        public Ship[] Ships { get; set; }
    }

    public class Ship
    {
        public string Id { get; set; }
        
        public string FlightPlanId { get; set; }
        public string Location { get; set; }

        public string Type { get; set; }
        public string Class { get; set; }
        public string Manufacturer { get; set; }

        public int SpaceAvailable { get; set; }
        public int MaxCargo { get; set; }

        public Cargo[] Cargo { get; set; }

        public int Speed { get; set; }
        public int Plating { get; set; }
        public int Weapons { get; set; }
    }

    public class Cargo
    {
        public string Good { get; set; }
        public int Quantity { get; set; }
        public int TotalVolume { get; set; }
    }

    public class FlightRequest 
    {
        public string ShipId { get; set; }
        public string Destination { get; set; }
    }

    public class WarpRequest
    {
        public string ShipId { get; set; }
    }

    public class FlightResponse
    {
        public FlightPlan FlightPlan { get; set; }
    }

    public class FlightPlan
    {
        public string Id { get; set; }
        public string ShipId { get; set; }
        public int FuelConsumed { get; set; }
        public int FuelRemaining { get; set; }
        public int TimeRemainingInSeconds { get; set; }
        public DateTimeOffset ArrivesAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? TerminatedAt { get; set; }
        public string Destination { get; set; }
        public string Departure { get; set; }
        public int Distance { get; set; }
    }
}
