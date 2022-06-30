using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Create.Subscription;

public class CreateAffiliateSubscriptionRequest : RequestBase
{
    public Guid? SubscriptionEntityGuid { get; set; }
    public string Value { get; set; }
    public string VerificationToken { get; set; }
}