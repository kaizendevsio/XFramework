using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Create;

public class CreateConnectionRequest : RequestBase
{
    public Guid? SourceCommunityIdentityGuid { get; set; }
    public Guid? TargetCommunityIdentityGuid { get; set; }
    public Guid? CommunityConnectionEntityGuid { get; set; }
}