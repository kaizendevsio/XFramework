using FluentValidation;
using POS.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.PosSettings;

public static class GetTenantPosSettingsEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    [MapGet("/api/pos/settings", Tags = ["POS Settings"], RequireAuthorization = true,
        RequiredActorCapabilities = [PosAuthorizationCapabilities.SalesView])]
    public static Task<Result<TenantPosSettingsResponse>> Handle(GetTenantPosSettingsRequest request,
        TenantPosSettingsService service, CancellationToken ct) => service.GetAsync(request, ct);
}

public static class UpdateTenantPosSettingsEndpoint
{
    [BoltHandler(TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredCrossTenantActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage],
        RequiredActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    [MapPost("/api/pos/settings", Tags = ["POS Settings"], RequireAuthorization = true,
        RequiredActorCapabilities = [XFrameworkActorCapabilities.IdentityTenantsManage])]
    public static Task<Result<TenantPosSettingsResponse>> Handle(UpdateTenantPosSettingsRequest request,
        TenantPosSettingsService service, CancellationToken ct) => service.UpdateAsync(request, ct);
}

public sealed class UpdateTenantPosSettingsValidator : AbstractValidator<UpdateTenantPosSettingsRequest>
{
    public UpdateTenantPosSettingsValidator() => RuleFor(x => x.Settings)
        .Must(x => x?.IsValid() == true).WithMessage("Use up to 20 checkout fields with unique keys and labels");
}
