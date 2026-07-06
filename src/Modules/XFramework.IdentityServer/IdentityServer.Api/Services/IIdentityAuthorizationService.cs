namespace IdentityServer.Api.Services;

public interface IIdentityAuthorizationService
{
    Task<Result<CredentialCapabilityCheckResponse>> CheckCredentialCapabilityAsync(
        CheckCredentialCapabilityRequest request,
        CancellationToken ct = default);

    Task<Result<EffectiveCredentialCapabilitiesResponse>> GetEffectiveCredentialCapabilitiesAsync(
        GetEffectiveCredentialCapabilitiesRequest request,
        CancellationToken ct = default);

    Task<Result<TenantAuthorizationPolicyResponse>> GetTenantAuthorizationPolicyAsync(
        GetTenantAuthorizationPolicyRequest request,
        CancellationToken ct = default);

    Task<Result<TenantAuthorizationPolicyResponse>> UpdateTenantAuthorizationPolicyAsync(
        UpdateTenantAuthorizationPolicyRequest request,
        CancellationToken ct = default);

    Task<Result<RoleTypePermissionsResponse>> GetRoleTypePermissionsAsync(
        GetRoleTypePermissionsRequest request,
        CancellationToken ct = default);

    Task<Result<RoleTypePermissionsResponse>> SetRoleTypePermissionsAsync(
        SetRoleTypePermissionsRequest request,
        CancellationToken ct = default);

    Task<Result<CredentialRolePermissionOverridesResponse>> GetCredentialRolePermissionOverridesAsync(
        GetCredentialRolePermissionOverridesRequest request,
        CancellationToken ct = default);

    Task<Result<CredentialRolePermissionOverridesResponse>> SetCredentialRolePermissionOverridesAsync(
        SetCredentialRolePermissionOverridesRequest request,
        CancellationToken ct = default);

    Task<Result<IdentityRole>> AssignCredentialRoleAsync(
        AssignCredentialRoleRequest request,
        CancellationToken ct = default);

    Task<Result> RemoveCredentialRoleAsync(
        RemoveCredentialRoleRequest request,
        CancellationToken ct = default);

    Task<Result> SeedRoleTypePermissionsAsync(
        Guid tenantId,
        Guid roleTypeId,
        CancellationToken ct = default);
}
