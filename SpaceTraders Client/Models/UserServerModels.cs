using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaceTraders_Client.Models
{
    public class DetailsResponse
    {
        public User User { get; set; }
    }
    public class User
    {
        public string Username { get; set; }
        public int Credits { get; set; }
        public Ship[] Ships { get; set; }
        public Loan[] Loans { get; set; }
    }
}
