using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Subscription;

namespace XFramework.Client.Shared.Core.Features.Member;

public partial class MemberState
{
    public class SetState : BaseAction
    {
        public List<SubscriptionResponse>? UnregisteredSubscriber { get; set; }
        public List<CredentialResponse>? MemberList { get; set; }
        public CredentialResponse? SelectedMember { get; set; }
        public string? SearchString { get; set; }
    }
} 
