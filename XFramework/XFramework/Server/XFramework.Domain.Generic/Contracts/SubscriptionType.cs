namespace XFramework.Domain.Generic.Contracts;

public partial record SubscriptionType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public virtual ICollection<Subscription> Subscriptions { get; } = new List<Subscription>();
}