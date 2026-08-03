using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace IdentityServer.Integration.Drivers;

// IdentityServer-local IBoltRequest methods are source-generated. Signing-key contracts
// are shared with service-token consumers, so this partial keeps them on the same wrapper.
public partial interface IIdentityServerServiceWrapper
{
    Task<QueryResponse<ServiceSigningKeysResponse>> GetServiceSigningKeys(
        GetServiceSigningKeysRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<ServiceSigningKeyResponse>> RotateServiceSigningKey(
        RotateServiceSigningKeyRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<ServiceSigningKeyResponse>> RetireServiceSigningKey(
        RetireServiceSigningKeyRequest request,
        CancellationToken ct = default);
}

public partial record IdentityServerServiceWrapper
{
    public Task<QueryResponse<ServiceSigningKeysResponse>> GetServiceSigningKeys(
        GetServiceSigningKeysRequest request,
        CancellationToken ct = default) =>
        SendAsync<GetServiceSigningKeysRequest, ServiceSigningKeysResponse>(request, ct);

    public Task<QueryResponse<ServiceSigningKeyResponse>> RotateServiceSigningKey(
        RotateServiceSigningKeyRequest request,
        CancellationToken ct = default) =>
        SendAsync<RotateServiceSigningKeyRequest, ServiceSigningKeyResponse>(request, ct);

    public Task<QueryResponse<ServiceSigningKeyResponse>> RetireServiceSigningKey(
        RetireServiceSigningKeyRequest request,
        CancellationToken ct = default) =>
        SendAsync<RetireServiceSigningKeyRequest, ServiceSigningKeyResponse>(request, ct);
}
