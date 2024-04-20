using IdentityServer.Domain.Shared.Contracts.Requests;

namespace XFramework.Blazor.Core.Features.Affiliate;

public partial class AffiliateState : State<AffiliateState>
{
    public override void Initialize()
    {
        
    }

    public CreateAffiliateSubscriptionRequest AffiliateSubscriptionVm { get; set; } = new();

}