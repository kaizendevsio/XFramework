namespace IdentityServer.Domain.Generic.Contracts.Responses.Verification;

public class IdentityVerificationSummaryResponse
{
    public bool IsVerified { get; set; }
    public DateTime? LastRequestExpiration { get; set; }
    public Guid? Guid { get; set; }
}