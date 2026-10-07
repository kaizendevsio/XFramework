using IdentityServer.Domain.Shared.Contracts;

namespace XFramework.Portal.Features.Identity.Services;

public sealed record TenantModuleFeatureDependencyIssue(
    TenantModuleFeatureDefinition Feature, TenantModuleFeatureEnablementPlan Plan);
