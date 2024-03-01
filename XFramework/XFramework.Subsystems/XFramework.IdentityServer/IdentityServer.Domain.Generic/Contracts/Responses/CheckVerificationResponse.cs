using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Domain.Generic.Contracts.Responses;

public record CheckVerificationResponse
{
    public bool IsVerified { get; init; }
    public IdentityVerification LastVerification { get; set; }
};