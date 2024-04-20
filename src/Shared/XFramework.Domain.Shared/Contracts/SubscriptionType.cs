using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class SubscriptionType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}