using XFramework.Domain.Shared.Contracts;

namespace IdentityServer.Domain.Shared.Contracts.Responses;

public record AuthenticateIdentityResponse
{
    public IdentityInformation? Identity { get; set; }
    public IdentityCredential? Credential { get; set; }
    public string? AccessToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
}