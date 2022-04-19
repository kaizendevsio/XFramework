using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Update;

public class UpdateIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? CommunityEntityGuid { get; set; }
    public Guid? Guid { get; set; }
}