namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record RefreshTokenResponse
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public Guid SessionId { get; set; }
    public int ExpiresIn { get; set; }
}
