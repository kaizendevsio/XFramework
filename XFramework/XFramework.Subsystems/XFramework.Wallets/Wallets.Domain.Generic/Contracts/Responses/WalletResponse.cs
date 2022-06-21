using System;

namespace Wallets.Domain.Generic.Contracts.Responses;

public class WalletResponse
{
    public Guid? Guid { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public long UserAuthId { get; set; }
    public long? WalletTypeId { get; set; }
    public decimal? Balance { get; set; }
    
    public virtual WalletEntityResponse WalletEntity { get; set; }
}