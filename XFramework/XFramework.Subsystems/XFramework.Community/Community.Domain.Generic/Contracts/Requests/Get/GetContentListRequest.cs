using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Get;

public class GetContentListRequest : RequestBase
{
    public Guid? ContentEntityGuid { get; set; }
    public Guid? CommunityIdentityGuid { get; set; }
}