namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class Subscription : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(1)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(2)]
    public string? Value { get; set; }

    [MemoryPackOrder(3)]
    public short? Status { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? ExpireAt { get; set; }

    [MemoryPackOrder(5)]
    public DateTime? CompletedAt { get; set; }


    [MemoryPackOrder(6)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(7)]
    public virtual SubscriptionType Type { get; set; } = null!;
}
