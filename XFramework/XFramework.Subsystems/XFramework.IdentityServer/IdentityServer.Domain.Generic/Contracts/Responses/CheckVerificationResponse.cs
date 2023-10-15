using XFramework.Domain.Generic.Contracts;

namespace IdentityServer.Domain.Generic.Contracts.Responses;

public record CheckVerificationResponse(
    bool IsVerified = default,
    IdentityVerification? LastVerification = default
);