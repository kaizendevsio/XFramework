namespace XFramework.Portal.Features.Identity.Services;

public sealed record TenantModuleFeatureEnablementPlan(
    IReadOnlyList<string> EnableKeys,
    IReadOnlyList<string> DisableKeys,
    IReadOnlyList<string> Errors);
