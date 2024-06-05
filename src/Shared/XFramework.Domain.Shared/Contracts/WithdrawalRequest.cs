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
    public decimal? TotalAmount { get; set; }

    [MemoryPackOrder(3)]
    public TransactionStatus WithdrawalStatus { get; set; }

    [MemoryPackOrder(4)]
    public string? Remarks { get; set; }

    [MemoryPackOrder(5)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(6)]
    public Guid WalletTypeId { get; set; }

    [MemoryPackOrder(7)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(8)]
    public virtual WalletType? WalletType { get; set; }

    [MemoryPackOrder(9)]
    public virtual Wallet? Wallet { get; set; }
}
