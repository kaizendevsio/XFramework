using BlazorBlueprint.Components;
using Inventario.Integration.Drivers;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Portal.Features.Inventario;

internal static class InventoryDisplay
{
    public static string Variant(IEnumerable<ProductVariation> variants, Guid? id) => id is null
        ? "Base product"
        : variants.FirstOrDefault(x => x.Id == id) is { } variant
            ? $"{variant.VariationType}: {variant.Name}".Trim(' ', ':')
            : $"Variant {id.ToString()![..8]}";

    // Date pickers represent a calendar date, not a local instant. Preserve that date at UTC midnight.
    public static DateTime? CalendarDateUtc(DateTime? value) => value is { } date
        ? DateTime.SpecifyKind(date.Date, DateTimeKind.Utc) : null;

    public static string Effect(InventoryMovementType type) => type is InventoryMovementType.Reservation or InventoryMovementType.Release
        ? "Reserved" : "On hand";

    public static string Reference(string? type) => type switch
    {
        "POS.SaleLine" => "POS sale line",
        "POS.ReturnLine" => "POS return line",
        "InitialReceipt" => "Initial receipt",
        "receiving" => "Stock receipt",
        null or "" => "No external reference",
        _ => type
    };

    public static async Task<InventarioSetupResponse> PreferencesAsync(IInventarioServiceWrapper inventario, Guid tenantId)
    {
        var result = await inventario.GetInventarioSetup(new GetInventarioSetupRequest { Metadata = new() { RequestedTenantId = tenantId } });
        if (!result.IsSuccess || result.Response is not { } preferences || preferences.TenantId != tenantId)
            throw new InvalidOperationException("Inventario preferences could not be loaded.");
        return preferences;
    }

    public static async Task<string> CurrencyAsync(IInventarioServiceWrapper inventario, Guid tenantId)
    {
        var preferences = await PreferencesAsync(inventario, tenantId);
        var code = preferences.DefaultCurrency.Trim().ToUpperInvariant();
        return CurrencyCatalog.GetAllCurrencyCodes().Contains(code) ? code : "PHP";
    }
}
