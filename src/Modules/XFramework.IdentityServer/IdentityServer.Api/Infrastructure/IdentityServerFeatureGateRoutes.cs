using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Services.FeatureGates;

namespace IdentityServer.Api.Infrastructure;

public static class IdentityServerFeatureGateRoutes
{
    public static void Configure(TenantModuleFeatureGateOptions options)
    {
        options.RequireFeature(TenantModuleFeatureKeys.IdentityUsers, "/api/identity-info");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityUsers, "/api/identities");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityCredentials, "/api/identity-credentials");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityCredentials, "/api/credentials");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityRoles, "/api/identity-roles");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityRoles, "/api/identity-role-types");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityRoles, "/api/identity-role-type-groups");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityRoles, "/api/identity-role-type-feature-permissions");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityTenants, "/api/tenants");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityTenants, "/api/tenant-module-features");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityTenants, "/api/identity/authorization/tenant-policy");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityRoles, "/api/identity-role-feature-permission-overrides");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityRoles, "/api/identity/authorization");
        options.RequireFeature(TenantModuleFeatureKeys.IdentitySessions, "/api/sessions");
        options.RequireFeature(TenantModuleFeatureKeys.IdentitySessions, "/api/session-types");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityVerifications, "/api/verifications");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityVerifications, "/api/identity-verifications");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityVerifications, "/api/identity-verification-types");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityContacts, "/api/identity-contacts");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityContacts, "/api/identity-contact-types");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityContacts, "/api/identity-contact-groups");
        options.RequireFeature(TenantModuleFeatureKeys.Identity, "/api/identity-favorites");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityAddresses, "/api/identity-addresses");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityAddresses, "/api/identity-address-types");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityAddresses, "/api/address-countries");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityAddresses, "/api/address-regions");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityAddresses, "/api/address-provinces");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityAddresses, "/api/address-cities");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityAddresses, "/api/address-barangays");
        options.RequireFeature(TenantModuleFeatureKeys.IdentityAuthLogs, "/api/authorization-logs");
        options.RequireFeature(TenantModuleFeatureKeys.Identity, "/api/registry-configurations");
        options.RequireFeature(TenantModuleFeatureKeys.Identity, "/api/registry-configuration-groups");
    }
}
