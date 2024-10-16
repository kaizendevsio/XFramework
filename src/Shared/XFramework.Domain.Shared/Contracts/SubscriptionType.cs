namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class SubscriptionType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string? Description { get; set; }


    [MemoryPackOrder(2)]
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
