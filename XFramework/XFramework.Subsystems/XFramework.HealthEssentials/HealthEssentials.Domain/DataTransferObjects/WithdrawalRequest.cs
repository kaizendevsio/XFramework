using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects
{
    public partial class WithdrawalRequest
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public long IdentityCredentialId { get; set; }
        public string? Address { get; set; }
        public decimal? TotalAmount { get; set; }
        public short? WithdrawalStatus { get; set; }
        public string? Remarks { get; set; }
        public long? SourceWalletTypeId { get; set; }
        public long? TargetCurrencyId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual IdentityCredential IdentityCredential { get; set; } = null!;
        public virtual WalletEntity? SourceWalletType { get; set; }
        public virtual WalletEntity? TargetCurrency { get; set; }
    }
}
