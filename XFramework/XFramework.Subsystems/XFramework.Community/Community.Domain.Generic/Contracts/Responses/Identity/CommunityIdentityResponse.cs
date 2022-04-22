using Community.Domain.Generic.Contracts.Responses.Connection;

namespace Community.Domain.Generic.Contracts.Responses.Identity;

public class CommunityIdentityResponse
{
    public string? HandleName { get; set; }
    public int Status { get; set; }
    public DateTime LastActive { get; set; }
    public string Guid { get; set; }
    public Guid? EntityGuid { get; set; }
    public Guid? IdentityCredentialGuid { get; set; }
    public string? Alias { get; set; }
    public string? Tagline { get; set; }
    public string? Description { get; set; }
    public CommunityIdentityFileResponse? Avatar { get; set; }
    public CommunityIdentityFileResponse? CoverPhoto { get; set; }
    
    public List<CommunityConnectionResponse>? ConnectionList { get; set; }
}