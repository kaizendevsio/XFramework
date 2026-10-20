using System.Text.Json;

namespace IdentityServer.Domain.Shared.Contracts;

[MemoryPackable]
public partial record TenantFinanceSettings
{
    public const string RegistryKey = "finance.settings.v1";
    public Guid DefaultCurrencyId { get; set; }
    public List<Guid> SupportedCurrencyIds { get; set; } = [];
    public bool TaxEnabled { get; set; }
    public decimal TaxRatePercent { get; set; }

    public bool IsValid() => DefaultCurrencyId != Guid.Empty &&
        SupportedCurrencyIds is { Count: > 0 and <= 500 } &&
        SupportedCurrencyIds.All(id => id != Guid.Empty) &&
        SupportedCurrencyIds.Distinct().Count() == SupportedCurrencyIds.Count &&
        SupportedCurrencyIds.Contains(DefaultCurrencyId) &&
        TaxRatePercent is >= 0 and <= 100;

    public decimal CalculateTax(decimal discountedSubtotal) => TaxEnabled
        ? decimal.Round(Math.Max(0, discountedSubtotal) * TaxRatePercent / 100m, 2, MidpointRounding.AwayFromZero)
        : 0m;

    // A null value means no configuration exists. Empty or corrupt explicit values fail closed.
    public static bool TryResolve(string? registryValue, IReadOnlyList<TenantFinanceCurrency> availableCurrencies,
        out TenantFinanceSettings? settings)
    {
        settings = null;
        var available = availableCurrencies.Where(x => x.Id != Guid.Empty && !string.IsNullOrWhiteSpace(x.Code))
            .OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Id).ToList();
        if (registryValue is null)
        {
            if (available.Count is 0 or > 500) return false;
            settings = new()
            {
                DefaultCurrencyId = available[0].Id,
                SupportedCurrencyIds = available.Select(x => x.Id).ToList()
            };
        }
        else
        {
            try { settings = JsonSerializer.Deserialize<TenantFinanceSettings>(registryValue); }
            catch (JsonException) { return false; }
        }
        return settings?.IsValid() == true && settings.SupportedCurrencyIds.All(id => available.Any(x => x.Id == id));
    }
}

[MemoryPackable]
public partial record TenantFinanceCurrency
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

[MemoryPackable]
public partial record TenantFinanceSettingsResponse
{
    public Guid TenantId { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public TenantFinanceSettings Settings { get; set; } = new();
    public List<TenantFinanceCurrency> SupportedCurrencies { get; set; } = [];
    public List<TenantFinanceCurrency> AvailableCurrencies { get; set; } = [];
    public bool CanManage { get; set; }
    // Consumers must reject repair drafts even when the authorized editor receives HTTP 200.
    public bool IsUsable { get; set; }
    public string? ConfigurationError { get; set; }
}
