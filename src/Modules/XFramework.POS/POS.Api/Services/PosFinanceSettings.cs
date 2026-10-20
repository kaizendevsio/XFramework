using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;

namespace POS.Api.Services;

public sealed class PosFinanceSettings(IIdentityServerServiceWrapper identity)
{
    public async Task<Result<TenantFinanceSettings>> GetAsync(RequestMetadata metadata, Guid tenantId, Guid currencyId, CancellationToken ct)
    {
        var result = await identity.GetTenantFinanceSettings(new GetTenantFinanceSettingsRequest { Metadata = metadata }, ct);
        if (!result.IsSuccess || result.Response is null)
            return Result<TenantFinanceSettings>.Failure(result.Message ?? "Tenant Finance settings are unavailable", result.IsSuccess ? 502 : (int)result.HttpStatusCode);
        if (result.Response.TenantId != tenantId)
            return Result<TenantFinanceSettings>.Forbidden("Finance settings do not match the trusted tenant");
        var settings = result.Response.Settings;
        if (!result.Response.IsUsable || !settings.IsValid() || !settings.SupportedCurrencyIds.Contains(currencyId))
            return Result<TenantFinanceSettings>.Conflict("POS currency is not supported by tenant Finance settings");
        return Result<TenantFinanceSettings>.Success(settings);
    }
}
