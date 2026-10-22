using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Patterns;

namespace POS.Api.Services;

public sealed class PosCheckoutSettingsService(AppDbContext db, IPosRequestContextResolver contextResolver)
{
    public async Task<Result<PosCheckoutSettingsResponse>> GetAsync(GetPosCheckoutSettingsRequest request, CancellationToken ct)
    {
        var context = contextResolver.Resolve(request);
        if (!context.IsSuccess)
            return Result<PosCheckoutSettingsResponse>.Failure(context.Message!, context.StatusCode);
        var result = await ReadAsync(context.Data!.TenantId, ct);
        return result.IsSuccess
            ? Result<PosCheckoutSettingsResponse>.Success(new() { TenantId = context.Data.TenantId, Settings = result.Data! })
            : Result<PosCheckoutSettingsResponse>.Failure(result.Message!, result.StatusCode);
    }

    public async Task<Result<PosCheckoutMetadata>> ValidateAsync(Guid tenantId, PosCheckoutMetadata metadata, bool requireValues, CancellationToken ct, bool requireTable = false)
    {
        var result = await ReadAsync(tenantId, ct);
        if (!result.IsSuccess) return Result<PosCheckoutMetadata>.Failure(result.Message!, result.StatusCode);
        var settings = result.Data!;
        if (settings.Validate(metadata, requireValues, requireTable) is { } error)
            return Result<PosCheckoutMetadata>.Failure(error, 400);
        var fields = metadata.Fields.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value.Trim());
        return Result<PosCheckoutMetadata>.Success(new()
        {
            TableNumber = settings.ActiveTableLayout?.Tables.FirstOrDefault(table => table.Id == metadata.TableId)?.Label.Trim(),
            TableId = metadata.TableId,
            TableLayoutId = metadata.TableLayoutId,
            Fields = fields,
            Labels = settings.CheckoutFields.Where(field => fields.ContainsKey(field.Key))
                .ToDictionary(field => field.Key, field => field.Label)
        });
    }

    private async Task<Result<PosCheckoutSettings>> ReadAsync(Guid tenantId, CancellationToken ct)
    {
        // Deliberate read-only tenant configuration projection; IdentityServer owns registry writes.
        var row = await db.Set<RegistryConfiguration>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Key == PosCheckoutSettings.RegistryKey)
            .Select(item => new { item.IsEnabled, item.Value }).SingleOrDefaultAsync(ct);
        if (row is null) return Result<PosCheckoutSettings>.Success(new());
        if (!row.IsEnabled) return Result<PosCheckoutSettings>.Conflict("POS checkout settings are disabled");
        try
        {
            var settings = JsonSerializer.Deserialize<PosCheckoutSettings>(row.Value ?? "");
            if (settings?.IsValid() == true) return Result<PosCheckoutSettings>.Success(settings);
        }
        catch (JsonException)
        {
            return Result<PosCheckoutSettings>.Conflict("POS checkout settings contain invalid JSON");
        }
        return Result<PosCheckoutSettings>.Conflict("POS checkout settings are invalid; ask an administrator to repair them");
    }
}
