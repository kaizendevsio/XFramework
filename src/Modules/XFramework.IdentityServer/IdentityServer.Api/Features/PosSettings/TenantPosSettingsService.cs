using System.Text.Json;
using POS.Domain.Shared.Contracts;

namespace IdentityServer.Api.Features.PosSettings;

public sealed class TenantPosSettingsService(AppDbContext db, ITrustedInvocationContextAccessor invocation,
    ILogger<TenantPosSettingsService> logger)
{
    private bool CanManage => invocation.Current?.Actor?.Capabilities.Contains("identity.tenants:manage") == true;

    public async Task<Result<TenantPosSettingsResponse>> GetAsync(GetTenantPosSettingsRequest request, CancellationToken ct)
    {
        var tenant = ResolveTenant(request);
        if (!tenant.IsSuccess) return Result<TenantPosSettingsResponse>.Failure(tenant.Message!, tenant.StatusCode);
        var row = await db.Set<RegistryConfiguration>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenant.Data && x.Key == PosCheckoutSettings.RegistryKey, ct);
        PosCheckoutSettings? settings = null;
        if (row is null) settings = new();
        else
        {
            try { settings = JsonSerializer.Deserialize<PosCheckoutSettings>(row.Value ?? ""); }
            catch (JsonException) { settings = null; }
        }
        var usable = row?.IsEnabled != false && settings?.IsValid() == true;
        if (!usable && !CanManage)
            return Result<TenantPosSettingsResponse>.Conflict("POS checkout settings are invalid or disabled");
        return Result<TenantPosSettingsResponse>.Success(new()
        {
            TenantId = tenant.Data, ConcurrencyStamp = row?.ConcurrencyStamp ?? Guid.Empty,
            Settings = settings?.IsValid() == true ? settings : new(), CanManage = CanManage, IsUsable = usable,
            ConfigurationError = usable ? null : "POS checkout settings are invalid or disabled; save valid settings to repair them"
        });
    }

    public async Task<Result<TenantPosSettingsResponse>> UpdateAsync(UpdateTenantPosSettingsRequest request, CancellationToken ct)
    {
        var tenant = ResolveTenant(request);
        if (!tenant.IsSuccess) return Result<TenantPosSettingsResponse>.Failure(tenant.Message!, tenant.StatusCode);
        if (!CanManage) return Result<TenantPosSettingsResponse>.Forbidden("Only a tenant administrator can change POS checkout settings");
        if (request.Settings?.IsValid() != true)
            return Result<TenantPosSettingsResponse>.Failure("Use up to 20 checkout fields with unique keys and labels", 400);
        var row = await db.Set<RegistryConfiguration>().AsTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenant.Data && x.Key == PosCheckoutSettings.RegistryKey, ct);
        if ((row?.ConcurrencyStamp ?? Guid.Empty) != request.ExpectedConcurrencyStamp)
            return Result<TenantPosSettingsResponse>.Conflict("POS settings changed; reload before saving");
        if (row is null)
        {
            var group = await db.Set<RegistryConfigurationGroup>()
                .FirstOrDefaultAsync(x => x.TenantId == tenant.Data && x.Name == "POS", ct);
            if (group is null)
            {
                group = new() { Id = Guid.NewGuid(), TenantId = tenant.Data, Name = "POS",
                    SystemReferenceId = Guid.NewGuid(), IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid() };
                db.Add(group);
            }
            row = new() { Id = Guid.NewGuid(), TenantId = tenant.Data, Key = PosCheckoutSettings.RegistryKey,
                GroupId = group.Id, CreatedAt = DateTime.UtcNow };
            db.Add(row);
        }
        row.Value = JsonSerializer.Serialize(request.Settings);
        row.IsEnabled = true;
        row.ConcurrencyStamp = Guid.NewGuid();
        row.ModifiedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Result<TenantPosSettingsResponse>.Conflict("POS settings changed; reload before saving");
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            return Result<TenantPosSettingsResponse>.Conflict("POS settings were created concurrently; reload before saving");
        }
        logger.LogInformation("Updated POS checkout settings for tenant {TenantId}", tenant.Data);
        return await GetAsync(new() { Metadata = request.Metadata }, ct);
    }

    private Result<Guid> ResolveTenant(RequestBase request)
    {
        var tenant = invocation.Current?.EffectiveTenantId;
        if (tenant is null || tenant == Guid.Empty) return Result<Guid>.Failure("Trusted tenant context is required", 400);
        if (request.Metadata?.RequestedTenantId is { } supplied && supplied != Guid.Empty && supplied != tenant)
            return Result<Guid>.Forbidden("Request tenant does not match trusted tenant context");
        return Result<Guid>.Success(tenant.Value);
    }
}
