using BlazorBlueprint.Components;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
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

    public static async Task<string> CurrencyAsync(IDataContext data, Guid tenantId)
    {
        var setting = await data.Query<RegistryConfiguration>().NoCache()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Key == "Settings:Inventario:DefaultCurrency")
            .FirstOrDefaultAsync();
        var code = setting?.Value?.Trim().ToUpperInvariant() ?? "PHP";
        return CurrencyCatalog.GetAllCurrencyCodes().Contains(code) ? code : "PHP";
    }
}
