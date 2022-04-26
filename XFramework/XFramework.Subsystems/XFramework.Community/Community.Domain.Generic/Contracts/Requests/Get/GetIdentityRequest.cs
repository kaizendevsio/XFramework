using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Get;

public class GetIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? CommunityIdentityGuid { get; set; }
}