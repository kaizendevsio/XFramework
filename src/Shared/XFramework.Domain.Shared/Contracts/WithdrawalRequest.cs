using XFramework.Domain.Shared.Enums;

namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class WithdrawalRequest : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public string? Address { get; set; }

    [MemoryPackOrder(2)]
    public decimal? Amount { get; set; }
    
    [MemoryPackOrder(3)]
    public decimal? Fee { get; set; }

    [MemoryPackOrder(4)]
    public TransactionStatus WithdrawalStatus { get; set; }

    [MemoryPackOrder(5)]
    public string? Remarks { get; set; }
    
    [MemoryPackOrder(6)]
    public string? ReferenceNumber { get; set; }

    [MemoryPackOrder(7)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(8)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(9)]
    public virtual Wallet? Wallet { get; set; }

}
