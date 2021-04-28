using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradeCommander.Models
{
    public class LoanRequest
    {
        public string Type { get; set; }
    }

    public class LoansAvailableResponse 
    { 
        public AvailableLoan[] Loans { get; set; }
    }

    public class AvailableLoan
    {
        public string Type { get; set; }
        public int Amount { get; set; }
        public bool CollateralRequired { get; set; }
        public double Rate { get; set; }
        public int TermInDays { get; set; }

    }

    public class LoansResponse
    {
        public Loan[] Loans { get; set; }
    }

    public class LoanResponse
    {
        public int Credits { get; set; }
        public Loan Loan { get; set; }
    }

    public class Loan 
    { 
        public string Id { get; set; }
        public DateTimeOffset Due { get; set; }
        public int RepaymentAmount { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
    }
}
