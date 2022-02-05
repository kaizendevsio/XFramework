using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserBillsPaymentTransaction
    {
        public long Id { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string Uuid { get; set; }
        public long? CredentialId { get; set; }
        public long? BillerId { get; set; }
        public string RawRequest { get; set; }
        public string RawResponse { get; set; }
        public int? Status { get; set; }
        public decimal? Amount { get; set; }
        public string ReferenceNo { get; set; }
        public decimal? Discount { get; set; }
        public decimal? ServiceCharge { get; set; }
        public decimal? TotalAmount { get; set; }
        public string ResponseReasonPhrase { get; set; }
        public int? ResponseHttpStatus { get; set; }
        public decimal? ConvenienceFee { get; set; }
        public string TempCredentialUid { get; set; }
        public long? IncomeTransactionId { get; set; }

        public virtual TblIdentityCredential Credential { get; set; }
        public virtual TblUserIncomeTransaction IncomeTransaction { get; set; }
    }
}
