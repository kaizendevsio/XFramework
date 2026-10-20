using XFramework.Integration.Attributes;
using FluentValidation;

namespace IdentityServer.Api.Features.FinanceSettings;

public static class GetTenantFinanceSettingsEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapGet("/api/finance/settings", Tags = ["Finance Settings"], RequireAuthorization = true)]
    public static Task<Result<TenantFinanceSettingsResponse>> Handle(
        GetTenantFinanceSettingsRequest request, TenantFinanceSettingsService service, CancellationToken ct) =>
        service.GetAsync(request, ct);
}

public static class UpdateTenantFinanceSettingsEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = ["wallets:manage"])]
    [MapPost("/api/finance/settings", Tags = ["Finance Settings"], RequireAuthorization = true,
        RequiredActorCapabilities = ["wallets:manage"])]
    public static Task<Result<TenantFinanceSettingsResponse>> Handle(
        UpdateTenantFinanceSettingsRequest request, TenantFinanceSettingsService service, CancellationToken ct) =>
        service.UpdateAsync(request, ct);
}

public sealed class UpdateTenantFinanceSettingsValidator : AbstractValidator<UpdateTenantFinanceSettingsRequest>
{
    public UpdateTenantFinanceSettingsValidator() => RuleFor(x => x.Settings)
        .Must(x => x?.IsValid() == true)
        .WithMessage("Choose supported currencies including the default, and a tax rate from 0 to 100");
}
