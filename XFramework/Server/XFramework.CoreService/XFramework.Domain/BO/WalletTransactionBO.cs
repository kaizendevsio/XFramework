using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Domain.Enums;

namespace XFramework.Domain.BO
{
    public class WalletTransactionBO
    {
        public double? Amount { get; set; }
        public UserAuthBO FromUser { get; set; }
        public UserAuthBO ToUser { get; set; }
        public decimal? AmountOpposite
        {
            get
            {
                this._AmountOppposite = Math.Abs((decimal)this.Amount) * -1;
                return this._AmountOppposite;
            }
            set { return; }
        }
        private decimal _AmountOppposite { get; set; }
        public string TxHash { get; set; }  
        public bool IsFeeEnabled { get; set; }
        public string Remarks { get; set; }
        public TransactionStatus TransactionStatus { get; set; }
        public TransactionType TransactionType { get; set; }
    }
}
