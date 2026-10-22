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

    public static string MovementName(InventoryMovementType type) => type == InventoryMovementType.OpeningBalance
        ? "Opening balance" : type.ToString();

    public static string MovementDescription(InventoryMovementType type) => type switch
    {
        InventoryMovementType.OpeningBalance => "Adds starting stock to on-hand and available quantities; does not replace the balance.",
        InventoryMovementType.Receipt => "Adds received stock to on-hand and available quantities.",
        InventoryMovementType.Adjustment => "Adds or removes on-hand and available stock. Enter a positive or negative quantity.",
        InventoryMovementType.Transfer => "Requires a source and destination; unavailable in this dialog.",
        InventoryMovementType.Reservation => "Increases reserved stock and reduces available stock; on-hand stays unchanged.",
        InventoryMovementType.Release => "Reduces reserved stock and increases available stock; on-hand stays unchanged.",
        InventoryMovementType.Shipment => "Reduces on-hand and available stock; does not release reserved stock.",
        InventoryMovementType.Return => "Adds returned stock to on-hand and available quantities.",
        _ => ""
    };

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
