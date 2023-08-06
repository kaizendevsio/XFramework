﻿namespace XFramework.Domain.Generic.Contracts;

public partial class SubscriptionType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    
    public virtual ICollection<Subscription> Subscriptions { get; } = new List<Subscription>();
}
