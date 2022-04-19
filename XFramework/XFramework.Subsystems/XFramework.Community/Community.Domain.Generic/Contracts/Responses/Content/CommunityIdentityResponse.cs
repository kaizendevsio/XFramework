namespace Community.Domain.Generic.Contracts.Responses.Content;

public class CommunityIdentityResponse
{
    public string? HandleName { get; set; }
    public int Status { get; set; }
    public DateTime LastActive { get; set; }
    public string Guid { get; set; } = null!;
    public Guid? EntityGuid { get; set; }
    public Guid? IdentityCredentialGuid { get; set; }
}