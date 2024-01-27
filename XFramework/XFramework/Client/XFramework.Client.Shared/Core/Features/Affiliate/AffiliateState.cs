using IdentityServer.Domain.Generic.Contracts.Requests;

namespace XFramework.Client.Shared.Core.Features.Affiliate;

public partial class AffiliateState : State<AffiliateState>
{
    public override void Initialize()
    {
        
    }

    public CreateAffiliateSubscriptionRequest AffiliateSubscriptionVm { get; set; } = new();

}