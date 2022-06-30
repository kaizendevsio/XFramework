using IdentityServer.Domain.Generic.Contracts.Requests.Create.Subscription;

namespace XFramework.Client.Shared.Core.Features.Affiliate;

public partial class AddressState
{
    public class SetState : IAction
    {

    }
    
    public CreateAffiliateSubscriptionRequest AffiliateSubscriptionVm { get; set; }
}