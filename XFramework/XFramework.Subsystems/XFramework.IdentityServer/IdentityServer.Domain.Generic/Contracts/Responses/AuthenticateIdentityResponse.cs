using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Domain.Generic.Contracts.Responses;

public record AuthenticateIdentityResponse(
    IdentityInformation? Identity = default,
    string? AccessToken = default,
    string? TokenType = default,
    int ExpiresIn = default,
    string? RefreshToken = default
);
