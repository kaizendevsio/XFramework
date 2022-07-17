using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Subscription;

namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState : State<MemberState>
{
    public override void Initialize()
    {
        
    }
    public List<SubscriptionResponse> UnregisteredSubscriber { get; set; } = new();
    public List<CredentialResponse> MemberList { get; set; } = new();
    public CredentialResponse SelectedMember { get; set; } = new();
    public string SearchString { get; set; } = string.Empty;
}