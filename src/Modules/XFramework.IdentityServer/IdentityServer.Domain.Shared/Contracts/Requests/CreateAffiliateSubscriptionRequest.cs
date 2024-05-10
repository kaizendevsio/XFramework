namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record CreateAffiliateSubscriptionRequest : RequestBase
{
    public Guid? SubscriptionEntityId { get; set; }
    public string? Value { get; set; }
    public string? VerificationToken { get; set; }
}