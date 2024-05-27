using System.ComponentModel.DataAnnotations.Schema;

namespace XFramework.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class WalletTransactionLineItem : BaseModel
{
    [MemoryPackOrder(0)]
    public decimal? Amount { get; set; }
        
    [MemoryPackOrder(1)]
    public decimal Fee { get; set; }
        
    [MemoryPackOrder(2)]
    public string? Description { get; set; }

    [MemoryPackOrder(3)]
    public Guid WalletTransactionId { get; set; }

    [ForeignKey(nameof(WalletTransactionId))]
    [MemoryPackOrder(4)]
    public virtual WalletTransaction WalletTransaction { get; set; } = null!;
}