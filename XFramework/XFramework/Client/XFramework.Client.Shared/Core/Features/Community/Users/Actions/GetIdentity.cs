using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetIdentity : NavigableRequest, IRequest<CmdResponse>
    {
        public Guid? CredentialGuid { get; set; }
        public Guid? CommunityIdentityGuid { get; set; }
    }
}