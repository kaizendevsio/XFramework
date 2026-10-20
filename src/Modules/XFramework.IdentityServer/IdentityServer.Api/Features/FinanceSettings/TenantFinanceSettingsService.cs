using System.Text.Json;
using XFramework.Domain.Shared.Contracts.Requests;
using CurrencyType = Wallets.Domain.Shared.Contracts.CurrencyType;

namespace IdentityServer.Api.Features.FinanceSettings;

public sealed class TenantFinanceSettingsService(
    AppDbContext db,
    ITrustedInvocationContextAccessor invocationAccessor,
    ILogger<TenantFinanceSettingsService> logger)
{
    public async Task<Result<TenantFinanceSettingsResponse>> GetAsync(GetTenantFinanceSettingsRequest request, CancellationToken ct)
    {
        var tenant = ResolveTenant(request);
        if (!tenant.IsSuccess)
            return Result<TenantFinanceSettingsResponse>.Failure(tenant.Message!, tenant.StatusCode);

        var row = await db.Set<RegistryConfiguration>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenant.Data && x.Key == TenantFinanceSettings.RegistryKey, ct);
        var available = await ReadCurrenciesAsync(tenant.Data, ct);
        var canManage = invocationAccessor.Current?.Actor?.Capabilities.Contains("wallets:manage") == true;
        if (row is null && available.Count == 0)
            return Result<TenantFinanceSettingsResponse>.Success(new()
            {
                TenantId = tenant.Data,
                CanManage = canManage,
                ConfigurationError = "Add an active currency before configuring Finance settings"
            });
        var resolved = TenantFinanceSettings.TryResolve(row is null ? null : row.Value ?? "", available, out var settings);
        if (row is { IsEnabled: false } || !resolved)
        {
            const string error = "Tenant Finance settings are invalid or disabled; select active currencies and save to repair";
            if (!canManage) return Result<TenantFinanceSettingsResponse>.Conflict(error);
            return Result<TenantFinanceSettingsResponse>.Success(new()
            {
                TenantId = tenant.Data, ConcurrencyStamp = row?.ConcurrencyStamp ?? Guid.Empty,
                Settings = settings ?? new(), AvailableCurrencies = available,
                CanManage = true, IsUsable = false, ConfigurationError = error
            });
        }
        return Result<TenantFinanceSettingsResponse>.Success(new()
        {
            TenantId = tenant.Data, ConcurrencyStamp = row?.ConcurrencyStamp ?? Guid.Empty,
            Settings = settings!, SupportedCurrencies = available.Where(x => settings!.SupportedCurrencyIds.Contains(x.Id)).ToList(),
            CanManage = canManage, IsUsable = true,
            AvailableCurrencies = available
        });
    }

    public async Task<Result<TenantFinanceSettingsResponse>> UpdateAsync(UpdateTenantFinanceSettingsRequest request, CancellationToken ct)
    {
        var tenant = ResolveTenant(request);
        if (!tenant.IsSuccess)
            return Result<TenantFinanceSettingsResponse>.Failure(tenant.Message!, tenant.StatusCode);
        if (invocationAccessor.Current?.Actor?.Capabilities.Contains("wallets:manage") != true)
            return Result<TenantFinanceSettingsResponse>.Forbidden("Finance settings require wallets:manage");
        if (request.Settings?.IsValid() != true)
            return Result<TenantFinanceSettingsResponse>.Failure("Choose supported currencies including the default, and a tax rate from 0 to 100", 400);

        var validated = await BuildResponseAsync(tenant.Data, Guid.Empty, request.Settings, ct);
        if (!validated.IsSuccess) return validated;
        var row = await db.Set<RegistryConfiguration>().AsTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenant.Data && x.Key == TenantFinanceSettings.RegistryKey, ct);
        if ((row?.ConcurrencyStamp ?? Guid.Empty) != request.ExpectedConcurrencyStamp)
            return Result<TenantFinanceSettingsResponse>.Conflict("Finance settings changed; reload before saving");
        if (row is null)
        {
            var group = await db.Set<RegistryConfigurationGroup>()
                .FirstOrDefaultAsync(x => x.TenantId == tenant.Data && x.Name == "Finance", ct);
            if (group is null)
            {
                group = new RegistryConfigurationGroup
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Data, Name = "Finance",
                    SystemReferenceId = Guid.NewGuid(), IsEnabled = true, CreatedAt = DateTime.UtcNow,
                    ConcurrencyStamp = Guid.NewGuid()
                };
                db.Add(group);
            }
            row = new RegistryConfiguration
            {
                Id = Guid.NewGuid(), TenantId = tenant.Data, Key = TenantFinanceSettings.RegistryKey,
                GroupId = group.Id, CreatedAt = DateTime.UtcNow
            };
            db.Add(row);
        }
        row.Value = JsonSerializer.Serialize(request.Settings);
        row.IsEnabled = true;
        row.ModifiedAt = DateTime.UtcNow;
        row.ConcurrencyStamp = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Result<TenantFinanceSettingsResponse>.Conflict("Finance settings changed; reload before saving");
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            return Result<TenantFinanceSettingsResponse>.Conflict("Finance settings were created concurrently; reload before saving");
        }
        logger.LogInformation("Updated Finance settings for tenant {TenantId}", tenant.Data);
        return await GetAsync(new GetTenantFinanceSettingsRequest { Metadata = request.Metadata }, ct);
    }

    private Result<Guid> ResolveTenant(RequestBase request)
    {
        var tenantId = invocationAccessor.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Failure("Trusted tenant context is required", 400);
        if (request.Metadata?.RequestedTenantId is { } supplied && supplied != Guid.Empty && supplied != tenantId)
            return Result<Guid>.Forbidden("Request tenant does not match trusted tenant context");
        return Result<Guid>.Success(tenantId.Value);
    }

    private async Task<Result<TenantFinanceSettingsResponse>> BuildResponseAsync(
        Guid tenantId, Guid stamp, TenantFinanceSettings settings, CancellationToken ct)
    {
        // Deliberate read-only reference-data projection. Only Wallets owns currency writes.
        var currencies = await db.Set<CurrencyType>().AsNoTracking()
            .Where(x => settings.SupportedCurrencyIds.Contains(x.Id) &&
                (x.TenantId == tenantId || x.TenantId == Guid.Empty) && x.IsEnabled == true && !x.IsDeleted)
            .OrderBy(x => x.CurrencyIsoCode3)
            .Select(x => new TenantFinanceCurrency { Id = x.Id, Code = x.CurrencyIsoCode3 ?? "", Name = x.Name ?? "" })
            .ToListAsync(ct);
        if (currencies.Count != settings.SupportedCurrencyIds.Count || currencies.Any(x => string.IsNullOrWhiteSpace(x.Code)))
            return Result<TenantFinanceSettingsResponse>.Conflict("Supported currencies must be active tenant or global currency records with a currency code");
        return Result<TenantFinanceSettingsResponse>.Success(new()
        {
            TenantId = tenantId, ConcurrencyStamp = stamp, Settings = settings, SupportedCurrencies = currencies
        });
    }

    private Task<List<TenantFinanceCurrency>> ReadCurrenciesAsync(Guid tenantId, CancellationToken ct) =>
        db.Set<CurrencyType>().AsNoTracking()
            .Where(x => (x.TenantId == tenantId || x.TenantId == Guid.Empty) && x.IsEnabled && !x.IsDeleted)
            .OrderBy(x => x.CurrencyIsoCode3).Take(501)
            .Select(x => new TenantFinanceCurrency { Id = x.Id, Code = x.CurrencyIsoCode3 ?? "", Name = x.Name ?? "" })
            .ToListAsync(ct);
}
