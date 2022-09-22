using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects
{
    public partial class WalletAddress
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public string? Address { get; set; }
        public decimal? Balance { get; set; }
        public string? Remarks { get; set; }
        public string Guid { get; set; } = null!;
        public long WalletId { get; set; }

        public virtual Wallet Wallet { get; set; } = null!;
    }
}
