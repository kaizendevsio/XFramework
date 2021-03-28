using System;
using XFramework.Domain.Enums;

namespace XFramework.Domain.BusinessObjects
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
