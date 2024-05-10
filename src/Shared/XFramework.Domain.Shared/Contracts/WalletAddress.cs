namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class WalletAddress : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Address { get; set; }

    [MemoryPackOrder(1)]
    public decimal? Balance { get; set; }

    [MemoryPackOrder(2)]
    public string? Remarks { get; set; }


    [MemoryPackOrder(3)]
    public Guid WalletId { get; set; }

    [MemoryPackOrder(4)]
    public virtual Wallet Wallet { get; set; } = null!;
}
