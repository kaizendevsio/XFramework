using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Get;

public class GetIdentityListRequest : RequestBase
{
    public int Limit { get; set; }
    public Guid? CommunityIdentityEntityGuid { get; set; }
}