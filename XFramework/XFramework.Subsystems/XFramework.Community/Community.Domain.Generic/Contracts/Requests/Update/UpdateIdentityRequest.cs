using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Update;

public class UpdateIdentityRequest : RequestBase
{
    public string? Alias { get; set; }
    public string? HandleName { get; set; }
    public string? Tagline { get; set; }
    public Guid? CredentialGuid { get; set; }
    public Guid? CommunityIdentityEntityGuid { get; set; }
    public Guid? Guid { get; set; }
}