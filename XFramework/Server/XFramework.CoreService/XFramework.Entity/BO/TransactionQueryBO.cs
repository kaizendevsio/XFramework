using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Data.Enums;

namespace XFramework.Data.BO
{
    public class TransactionQueryBO
    {
        public string FilterString { get; set; }
        public TransactionType TransactionType { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public SortingType SortingType { get; set; }
    }
}
