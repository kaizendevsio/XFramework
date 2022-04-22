using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Get;

public class GetConnectionListRequest : RequestBase
{
    public int Limit { get; set; }
    public Guid? CommunityIdentityGuid { get; set; }
    public Guid? ConnectionEntityGuid { get; set; }
}