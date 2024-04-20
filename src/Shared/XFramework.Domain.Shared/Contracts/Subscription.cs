using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class Subscription : BaseModel
{
    public Guid TypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? Value { get; set; }

    public short? Status { get; set; }

    public DateTime? ExpireAt { get; set; }

    public DateTime? CompletedAt { get; set; }


    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual SubscriptionType Type { get; set; } = null!;
}