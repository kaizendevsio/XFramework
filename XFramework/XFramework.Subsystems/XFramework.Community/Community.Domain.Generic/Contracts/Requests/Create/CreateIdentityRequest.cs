using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Create;

public class CreateIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? CommunityEntityGuid { get; set; }
}