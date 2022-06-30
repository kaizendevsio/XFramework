using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Create;

public class CreateIdentityRequest : RequestBase
{
    public string? Alias { get; set; }
    public string? HandleName { get; set; }
    public string? Tagline { get; set; }
    public Guid? CredentialGuid { get; set; }
    public Guid? CommunityEntityGuid { get; set; }
}