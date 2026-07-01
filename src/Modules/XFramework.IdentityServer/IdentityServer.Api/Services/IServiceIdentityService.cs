using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;

namespace IdentityServer.Api.Services;

public interface IServiceIdentityService
{
    Task<Result<ServiceTokenResponse>> IssueTokenAsync(IssueServiceTokenRequest request, CancellationToken ct = default);
    Task<Result<ServiceSigningKeysResponse>> GetSigningKeysAsync(GetServiceSigningKeysRequest request, CancellationToken ct = default);
    Task<Result<ServiceSigningKeyResponse>> RotateSigningKeyAsync(RotateServiceSigningKeyRequest request, CancellationToken ct = default);
    Task<Result<ServiceSigningKeyResponse>> RetireSigningKeyAsync(RetireServiceSigningKeyRequest request, CancellationToken ct = default);
}
