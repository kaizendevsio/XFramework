using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Domain.Generic.Contracts.Responses;

public record AuthenticateIdentityResponse
{
    public IdentityInformation? Identity { get; set; }
    public string? AccessToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
}