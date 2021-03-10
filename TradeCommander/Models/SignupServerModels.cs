using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class SignupResponse
    {
        public string Token { get; set; }
        public SignupUser User { get; set; }
    }
    public class SignupUser
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Picture { get; set; }
        public string Email { get; set; }
        public int Credits { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
