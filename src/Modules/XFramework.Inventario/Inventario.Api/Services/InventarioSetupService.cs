using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Api.Services;

public sealed class InventarioSetupService(
    DbContext db,
    ITrustedInvocationContextAccessor invocation,
    ITenantModuleFeatureService features,
    ILogger<InventarioSetupService> logger)
{
    public async Task<Result<InventarioSetupResponse>> GetAsync(GetInventarioSetupRequest request, CancellationToken ct = default)
    {
        var access = await AccessAsync(request, false, ct);
        return access.IsSuccess
            ? Result<InventarioSetupResponse>.Success(await ReadAsync(access.Data, ct))
            : Result<InventarioSetupResponse>.Failure(access.Message!, access.StatusCode);
    }

    public async Task<Result<InventarioSetupResponse>> CompleteAsync(CompleteInventarioSetupRequest request, CancellationToken ct = default)
    {
        var access = await AccessAsync(request, true, ct);
        if (!access.IsSuccess) return Result<InventarioSetupResponse>.Failure(access.Message!, access.StatusCode);
        if (request.CompletionRequestId == Guid.Empty || !Enum.IsDefined(request.Mode) ||
            !ValidPreferences(request.LowStockThreshold, request.DefaultCurrency))
            return Result<InventarioSetupResponse>.Failure("Choose a setup mode and valid catalog defaults.", 400);

        var tenantId = access.Data;
        var hash = CompletionHash(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize this tenant's setup/preferences across service instances, not just one browser.
        await LockTenantAsync(tenantId, ct);
        var setup = await db.Set<InventarioSetup>().AsTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (setup?.CompletedAt is not null)
            return setup.CompletionHash == hash
                ? Result<InventarioSetupResponse>.Success(await ReadAsync(tenantId, ct), message: "Setup already completed.")
                : Result<InventarioSetupResponse>.Conflict("Setup already completed. Open Settings to change existing configuration.");
        if (setup?.ConcurrencyStamp != request.ExpectedConcurrencyStamp)
            return Result<InventarioSetupResponse>.Conflict("Settings changed. Reload setup before confirming.");

        var before = await ReadAsync(tenantId, ct);
        if (request.Mode == InventarioSetupMode.Basic && before.HasExistingConfiguration)
            return Result<InventarioSetupResponse>.Conflict("Existing configuration was found. Use advanced setup to keep existing records.");
        if (request.Mode == InventarioSetupMode.Basic && !before.WarehousingEnabled)
            return Result<InventarioSetupResponse>.Forbidden("Warehousing must be enabled before basic setup.");

        Warehouse? warehouse = null;
        InventoryLocation? location = null;
        if (before.WarehousingEnabled)
        {
            if (request.ExistingWarehouseId is { } warehouseId)
            {
                if (request.Warehouse is not null)
                    return Result<InventarioSetupResponse>.Failure("Choose an existing warehouse or a new warehouse, not both.", 400);
                warehouse = await db.Set<Warehouse>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == warehouseId && x.IsEnabled, ct);
                if (warehouse is null) return Result<InventarioSetupResponse>.NotFound("Selected warehouse is unavailable.");
            }
            else
            {
                var options = request.Warehouse;
                if (options is null || !Required(options.Code, 50) || !Required(options.Name, 200) ||
                    !Optional(options.Description, 1000) || !Optional(options.AddressLine, 500) ||
                    !Optional(options.City, 100) || !Optional(options.Region, 100) || !Optional(options.PostalCode, 25) ||
                    (!string.IsNullOrWhiteSpace(options.CountryCode) && options.CountryCode.Trim().Length != 2))
                    return Result<InventarioSetupResponse>.Failure("Enter valid warehouse details.", 400);
                if (await db.Set<Warehouse>().AnyAsync(x => x.TenantId == tenantId && x.Code == options.Code!.Trim(), ct))
                    return Result<InventarioSetupResponse>.Conflict("Warehouse code already exists. Select the existing warehouse.");
                warehouse = new Warehouse
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, Code = options.Code!.Trim(), Name = options.Name!.Trim(),
                    Description = Trim(options.Description), AddressLine = Trim(options.AddressLine), City = Trim(options.City),
                    Region = Trim(options.Region), PostalCode = Trim(options.PostalCode), CountryCode = Trim(options.CountryCode)?.ToUpperInvariant(),
                    IsDefault = options.IsDefault && !await db.Set<Warehouse>().AnyAsync(x => x.TenantId == tenantId && x.IsDefault, ct),
                    IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                };
            }
            if (request.ExistingLocationId is { } locationId)
            {
                if (request.Location is not null)
                    return Result<InventarioSetupResponse>.Failure("Choose an existing location or a new location, not both.", 400);
                location = await db.Set<InventoryLocation>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == locationId && x.WarehouseId == warehouse.Id && x.IsEnabled, ct);
                if (location is null) return Result<InventarioSetupResponse>.NotFound("Selected location is unavailable in this warehouse.");
            }
            else
            {
                var options = request.Location;
                if (options is null || !Required(options.Code, 50) || !Required(options.Name, 200) ||
                    !Optional(options.Description, 1000) || !Enum.IsDefined(options.LocationType))
                    return Result<InventarioSetupResponse>.Failure("Enter valid location details.", 400);
                if (options.ParentLocationId is { } parentId && !await db.Set<InventoryLocation>()
                    .AnyAsync(x => x.TenantId == tenantId && x.Id == parentId && x.WarehouseId == warehouse.Id && x.IsEnabled, ct))
                    return Result<InventarioSetupResponse>.NotFound("Parent location is unavailable in this warehouse.");
                if (await db.Set<InventoryLocation>().AnyAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouse.Id && x.Code == options.Code!.Trim(), ct))
                    return Result<InventarioSetupResponse>.Conflict("Location code already exists. Select the existing location.");
                location = new InventoryLocation
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id, Code = options.Code!.Trim(), Name = options.Name!.Trim(),
                    Description = Trim(options.Description), ParentLocationId = options.ParentLocationId, LocationType = options.LocationType,
                    IsPickable = options.IsPickable, IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                };
            }
        }
        else if (request.Warehouse is not null || request.Location is not null || request.ExistingWarehouseId is not null || request.ExistingLocationId is not null)
            return Result<InventarioSetupResponse>.Forbidden("Warehousing is disabled for this tenant.");

        // Stage only after all validation passes; warehouse, location, defaults and completion commit together.
        if (warehouse is not null && request.ExistingWarehouseId is null) db.Add(warehouse);
        if (location is not null && request.ExistingLocationId is null) db.Add(location);
        if (setup is null)
        {
            setup = NewSetup(tenantId);
            db.Add(setup);
        }
        setup.Mode = request.Mode;
        setup.CompletedAt = DateTime.UtcNow;
        setup.CompletionRequestId = request.CompletionRequestId;
        setup.CompletionHash = hash;
        setup.WarehouseId = warehouse?.Id;
        setup.LocationId = location?.Id;
        SetPreferences(setup, request.LowStockThreshold, request.DefaultCurrency);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Inventario setup conflicted for tenant {TenantId}.", tenantId);
            return Result<InventarioSetupResponse>.Conflict("Configuration changed. Reload before confirming setup.");
        }
        return Result<InventarioSetupResponse>.Success(await ReadAsync(tenantId, ct));
    }

    public async Task<Result<InventarioSetupResponse>> UpdatePreferencesAsync(UpdateInventarioPreferencesRequest request, CancellationToken ct = default)
    {
        var access = await AccessAsync(request, true, ct);
        if (!access.IsSuccess) return Result<InventarioSetupResponse>.Failure(access.Message!, access.StatusCode);
        if (!ValidPreferences(request.LowStockThreshold, request.DefaultCurrency))
            return Result<InventarioSetupResponse>.Failure("Enter a non-negative threshold and supported currency code.", 400);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockTenantAsync(access.Data, ct);
        var setup = await db.Set<InventarioSetup>().AsTracking().SingleOrDefaultAsync(x => x.TenantId == access.Data, ct);
        if (setup?.ConcurrencyStamp != request.ExpectedConcurrencyStamp)
            return Result<InventarioSetupResponse>.Conflict("Settings changed. Reload before saving.");
        if (setup is null)
        {
            setup = NewSetup(access.Data);
            setup.Mode = InventarioSetupMode.Advanced;
            db.Add(setup);
        }
        SetPreferences(setup, request.LowStockThreshold, request.DefaultCurrency);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<InventarioSetupResponse>.Success(await ReadAsync(access.Data, ct));
    }

    private async Task<InventarioSetupResponse> ReadAsync(Guid tenantId, CancellationToken ct)
    {
        var setup = await db.Set<InventarioSetup>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId, ct);
        // Read legacy preferences for compatibility; never write Identity's schema from Inventario.
        var legacy = await db.Set<RegistryConfiguration>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Key.StartsWith("Settings:Inventario:"))
            .Select(x => new { x.Key, x.Value }).Take(50).ToListAsync(ct);
        var warehousing = (await features.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.WarehousingSubFeature, ct)).IsSuccess;
        var warehouses = warehousing ? await db.Set<Warehouse>().AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Code).Take(200).ToListAsync(ct) : [];
        var locations = warehousing ? await db.Set<InventoryLocation>().AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Code).Take(500).ToListAsync(ct) : [];
        var existing = setup is not null || legacy.Count > 0 ||
            await db.Set<Warehouse>().AnyAsync(x => x.TenantId == tenantId, ct) ||
            await db.Set<Product>().AnyAsync(x => x.TenantId == tenantId, ct) ||
            await db.Set<ProductCategory>().AnyAsync(x => x.TenantId == tenantId, ct) ||
            await db.Set<Supplier>().AnyAsync(x => x.TenantId == tenantId, ct);
        var threshold = int.TryParse(legacy.FirstOrDefault(x => x.Key == "Settings:Inventario:LowStockThreshold")?.Value, out var value) && value >= 0 ? value : 5;
        return new InventarioSetupResponse
        {
            TenantId = tenantId, ConcurrencyStamp = setup?.ConcurrencyStamp, Mode = setup?.Mode, CompletedAt = setup?.CompletedAt,
            HasExistingConfiguration = existing, WarehousingEnabled = warehousing, CanManage = CanManage(),
            LowStockThreshold = setup?.LowStockThreshold ?? threshold,
            DefaultCurrency = setup?.DefaultCurrency ?? legacy.FirstOrDefault(x => x.Key == "Settings:Inventario:DefaultCurrency")?.Value ?? "PHP",
            WarehouseId = setup?.WarehouseId, LocationId = setup?.LocationId, Warehouses = warehouses, Locations = locations
        };
    }

    private async Task<Result<Guid>> AccessAsync(RequestBase request, bool write, CancellationToken ct)
    {
        var tenantId = invocation.Current?.EffectiveTenantId;
        if (invocation.Current?.Actor is null || tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Sign in and select a tenant.");
        if (request.Metadata.RequestedTenantId is { } requested && requested != tenantId)
            return Result<Guid>.Forbidden("The requested tenant does not match the authorized tenant.");
        if (write && !CanManage()) return Result<Guid>.Forbidden("Tenant management permission is required for setup.");
        var gate = await features.EnsureEnabledAsync(tenantId.Value, TenantModuleFeatureKeys.Inventario, ct: ct);
        return gate.IsSuccess ? Result<Guid>.Success(tenantId.Value) : Result<Guid>.Failure(gate.Message!, gate.StatusCode);
    }

    private bool CanManage() => invocation.Current?.Actor?.Capabilities.Contains(XFrameworkActorCapabilities.IdentityTenantsManage) == true;
    private Task LockTenantAsync(Guid tenantId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"inventario:setup:" + tenantId}, 0))", ct);
    private static InventarioSetup NewSetup(Guid tenantId) => new() { Id = Guid.NewGuid(), TenantId = tenantId, IsEnabled = true, CreatedAt = DateTime.UtcNow };
    private static void SetPreferences(InventarioSetup setup, int threshold, string currency)
    {
        setup.LowStockThreshold = threshold;
        setup.DefaultCurrency = currency.Trim().ToUpperInvariant();
        setup.ModifiedAt = DateTime.UtcNow;
        setup.ConcurrencyStamp = Guid.NewGuid();
    }
    private static bool Required(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
    private static bool Optional(string? value, int max) => value?.Trim().Length is null || value.Trim().Length <= max;
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ValidPreferences(int threshold, string currency) => threshold >= 0 &&
        !string.IsNullOrWhiteSpace(currency) && CurrencyCodes.Contains(currency.Trim().ToUpperInvariant());
    private static readonly HashSet<string> CurrencyCodes = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(x => new RegionInfo(x.Name).ISOCurrencySymbol).ToHashSet(StringComparer.Ordinal);
    private static string CompletionHash(CompleteInventarioSetupRequest request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        request.Mode, request.ExistingWarehouseId, request.ExistingLocationId, request.LowStockThreshold,
        Currency = request.DefaultCurrency.Trim().ToUpperInvariant(),
        Warehouse = request.Warehouse is { } w ? new { w.Code, w.Name, w.Description, w.AddressLine, w.City, w.Region, w.PostalCode, w.CountryCode, w.IsDefault } : null,
        Location = request.Location is { } l ? new { l.Code, l.Name, l.Description, l.ParentLocationId, l.LocationType, l.IsPickable } : null
    })));
}
