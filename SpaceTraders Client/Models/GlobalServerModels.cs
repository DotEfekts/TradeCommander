using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaceTraders_Client.Models
{
    public class ErrorResponse
    {
        public Error Error { get; set; }
    }

    public class Error 
    { 
        public int Code { get; set; }
        public string Message { get; set; }
    }
}
