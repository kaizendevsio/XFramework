using XFramework.Domain.Shared.Contracts;

namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record CheckVerificationResponse
{
    public bool IsVerified { get; init; }
    public IdentityVerification LastVerification { get; set; }
};