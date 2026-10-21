namespace POS.Domain.Shared.Contracts;

[MemoryPackable]
public partial record PosCheckoutSettings
{
    public const string RegistryKey = "pos.checkout.settings";
    public bool TableTrackingEnabled { get; set; }
    public List<PosCheckoutField> CheckoutFields { get; set; } = [];

    public bool IsValid() => CheckoutFields is { Count: <= 20 } &&
        CheckoutFields.All(field => field is not null &&
            !string.IsNullOrWhiteSpace(field.Key) && field.Key.Length <= 64 &&
            field.Key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') &&
            !string.IsNullOrWhiteSpace(field.Label) && field.Label.Length <= 100) &&
        CheckoutFields.Select(field => field.Key).Distinct(StringComparer.Ordinal).Count() == CheckoutFields.Count;

    public string? Validate(PosCheckoutMetadata? metadata, bool requireValues)
    {
        if (!IsValid()) return "POS checkout settings are invalid";
        if (metadata is null || metadata.Fields is null) return "Checkout details are required";
        if (metadata.TableNumber?.Length > 64) return "Table number must not exceed 64 characters";
        if (!TableTrackingEnabled && !string.IsNullOrWhiteSpace(metadata.TableNumber))
            return "Table tracking is disabled";
        if (metadata.Fields.Count > 20) return "Too many checkout fields";
        foreach (var (key, value) in metadata.Fields)
        {
            var field = CheckoutFields.FirstOrDefault(item => item.Key == key && item.Enabled);
            if (field is null) return "An unknown or disabled checkout field was supplied";
            if (value?.Length > 500) return $"{field.Label} must not exceed 500 characters";
        }
        foreach (var field in CheckoutFields.Where(item => item.Enabled && item.Required))
            if (requireValues && (!metadata.Fields.TryGetValue(field.Key, out var value) || string.IsNullOrWhiteSpace(value)))
                return $"{field.Label} is required";
        return null;
    }
}

[MemoryPackable]
public partial record PosCheckoutField
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool Required { get; set; }
}

[MemoryPackable]
public partial record PosCheckoutMetadata
{
    public string? TableNumber { get; set; }
    public Dictionary<string, string> Fields { get; set; } = [];
    // Saved labels keep historical receipts readable after settings are renamed.
    public Dictionary<string, string> Labels { get; set; } = [];
}

[MemoryPackable]
public partial record PosCheckoutSettingsResponse
{
    public Guid TenantId { get; set; }
    public PosCheckoutSettings Settings { get; set; } = new();
}
