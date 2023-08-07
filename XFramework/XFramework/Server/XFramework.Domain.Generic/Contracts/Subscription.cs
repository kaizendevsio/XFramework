namespace XFramework.Domain.Generic.Contracts;

public partial class Subscription
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? Value { get; set; }

    public short? Status { get; set; }

    public DateTime? ExpireAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    
    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual SubscriptionType Type { get; set; } = null!;
}
