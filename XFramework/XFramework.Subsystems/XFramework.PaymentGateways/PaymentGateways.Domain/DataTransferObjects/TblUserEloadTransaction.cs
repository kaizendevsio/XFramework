using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblUserEloadTransaction
    {
        public long Id { get; set; }
        public long? TelcoProductCodeId { get; set; }
        public decimal? Discount { get; set; }
        public string SenderNumber { get; set; }
        public string CustomerNumber { get; set; }
        public decimal? PreviousBalance { get; set; }
        public decimal? CurrentBalance { get; set; }
        public string TransactionId { get; set; }
        public long? WalletTypeId { get; set; }
        public string RawRequest { get; set; }
        public string RawResponse { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }
        public int? Status { get; set; }
        public string Amount { get; set; }
    }
}
