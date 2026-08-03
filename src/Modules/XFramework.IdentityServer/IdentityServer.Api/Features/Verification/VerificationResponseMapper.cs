namespace IdentityServer.Api.Features.Verification;

internal static class VerificationResponseMapper
{
    public static Result<VerificationAdministrationResponse> Map(
        Result<IdentityVerification> result)
    {
        if (!result.IsSuccess || result.Data is null)
        {
            return Result<VerificationAdministrationResponse>.Failure(
                result.Message ?? "Verification operation failed",
                result.StatusCode);
        }

        var verification = result.Data;
        return Result<VerificationAdministrationResponse>.Success(
            new VerificationAdministrationResponse
            {
                Id = verification.Id,
                TenantId = verification.TenantId,
                CredentialId = verification.CredentialId,
                VerificationTypeId = verification.VerificationTypeId,
                Status = verification.Status,
                StatusUpdatedOn = verification.StatusUpdatedOn,
                Expiry = verification.Expiry,
                ConsumedAt = verification.ConsumedAt,
                Purpose = verification.Purpose,
                FailedAttempts = verification.FailedAttempts,
                IsEnabled = verification.IsEnabled,
                ConcurrencyStamp = verification.ConcurrencyStamp,
                CreatedAt = verification.CreatedAt,
                ModifiedAt = verification.ModifiedAt
            },
            result.StatusCode,
            result.Message);
    }
}
