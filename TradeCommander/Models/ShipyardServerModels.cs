using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class ShipyardPurchaseRequest 
    { 
        public string Location { get; set; }
        public string Type { get; set; }
    }

    public class ShipyardResponse
    {
        public ShipyardListing[] ShipListings { get; set; }
    }

    public class ShipyardListing
    {

        public string Type { get; set; }
        public string Class { get; set; }
        public string Manufacturer { get; set; }
        public int MaxCargo { get; set; }
        public int Speed { get; set; }
        public int Plating { get; set; }
        public int Weapons { get; set; }
        public PurchaseLocation[] PurchaseLocations { get; set; }
    }

    public class PurchaseLocation
    {
        public string Location { get; set; }
        public int Price { get; set; }
        public string System { get; set; }
    }
}
