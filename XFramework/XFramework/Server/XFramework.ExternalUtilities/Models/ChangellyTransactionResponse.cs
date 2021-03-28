using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework.External.Models
{
   public class ChangellyTransactionResponse
    {
        public string id { get; set; }
        public string apiExtraFee { get; set; }
        public string changellyFee { get; set; }
        public object payinExtraId { get; set; }
        public object payoutExtraId { get; set; }
        public string amountExpectedFrom { get; set; }
        public string status { get; set; }
        public string currencyFrom { get; set; }
        public string currencyTo { get; set; }
        public int amountTo { get; set; }
        public string amountExpectedTo { get; set; }
        public string payinAddress { get; set; }
        public string payoutAddress { get; set; }
        public string createdAt { get; set; }
        public bool kycRequired { get; set; }
    }
}
