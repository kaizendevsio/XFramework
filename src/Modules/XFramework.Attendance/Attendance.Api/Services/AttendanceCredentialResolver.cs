using IdentityServer.Integration.Drivers;
using XFramework.Core.Patterns;

namespace Attendance.Api.Services;

public sealed record AttendanceCredentialSnapshot(
    Guid CredentialId,
    Guid TenantId,
    bool IsEnabled,
    bool IsDeleted,
    string? UserAlias,
    string? UserName);

public interface IAttendanceCredentialResolver
{
    Task<Result<AttendanceCredentialSnapshot>> ResolveAsync(
        Guid credentialId,
        Guid tenantId,
        CancellationToken ct);
}

public sealed class AttendanceCredentialResolver(
    IIdentityServerServiceWrapper identityServer,
    ILogger<AttendanceCredentialResolver> logger)
    : IAttendanceCredentialResolver
{
    public async Task<Result<AttendanceCredentialSnapshot>> ResolveAsync(
        Guid credentialId,
        Guid tenantId,
        CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var response = await identityServer.IdentityCredential.Get(
                credentialId,
                tenantId,
                noCache: true,
                navigationDepth: 0,
                includeNavigations: false);
            ct.ThrowIfCancellationRequested();

            if (!response.IsSuccess)
            {
                var statusCode = (int)response.HttpStatusCode;
                return statusCode == 404
                    ? Result<AttendanceCredentialSnapshot>.NotFound("Identity credential was not found")
                    : Result<AttendanceCredentialSnapshot>.Failure(
                        response.Message ?? "IdentityServer credential validation failed",
                        statusCode >= 500 ? 503 : statusCode);
            }

            var credential = response.Response;
            if (credential is null)
                return Result<AttendanceCredentialSnapshot>.NotFound("Identity credential was not found");

            return Result<AttendanceCredentialSnapshot>.Success(new AttendanceCredentialSnapshot(
                credential.Id,
                credential.TenantId,
                credential.IsEnabled,
                credential.IsDeleted,
                credential.UserAlias,
                credential.UserName));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "IdentityServer credential validation failed for credential {CredentialId} in tenant {TenantId}",
                credentialId,
                tenantId);
            return Result<AttendanceCredentialSnapshot>.Failure("IdentityServer is unavailable", 503);
        }
    }
}
