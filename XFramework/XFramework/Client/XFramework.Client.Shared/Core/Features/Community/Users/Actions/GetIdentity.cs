namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetIdentity : IRequest<CmdResponse>
    {
        public string NavigateToOnSuccess { get; set; }
        public string NavigateToOnFailure { get; set; }
        public Guid? CredentialGuid { get; set; }
        public Guid? CommunityIdentityGuid { get; set; }
    }
}