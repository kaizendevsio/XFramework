namespace POS.Domain.Shared.Contracts;

[MemoryPackable]
public partial record PosCheckoutSettings
{
    public const string RegistryKey = "pos.checkout.settings";
    public bool TableTrackingEnabled { get; set; }
    public List<PosCheckoutField> CheckoutFields { get; set; } = [];
    public bool TableRequired { get; set; }
    public Guid? ActiveTableLayoutId { get; set; }
    public List<PosTableLayout> TableLayouts { get; set; } = [];
    public PosReceiptSettings Receipt { get; set; } = new();

    [MemoryPackIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public PosTableLayout? ActiveTableLayout => TableLayouts.FirstOrDefault(layout => layout.Id == ActiveTableLayoutId);

    public PosCheckoutSettings DeepClone() => this with
    {
        CheckoutFields = CheckoutFields.Select(field => field with { }).ToList(),
        TableLayouts = TableLayouts.Select(layout => layout.DeepClone()).ToList(),
        Receipt = Receipt.DeepClone()
    };

    public bool IsValid() => CheckoutFields is { Count: <= 20 } &&
        CheckoutFields.All(field => field is not null &&
            !string.IsNullOrWhiteSpace(field.Key) && field.Key.Length <= 64 &&
            field.Key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') &&
            !string.IsNullOrWhiteSpace(field.Label) && field.Label.Length <= 100) &&
        CheckoutFields.Select(field => field.Key).Distinct(StringComparer.Ordinal).Count() == CheckoutFields.Count &&
        TableLayouts is { Count: <= 10 } && TableLayouts.All(layout => layout is not null && layout.IsValid()) &&
        TableLayouts.Select(layout => layout.Id).Distinct().Count() == TableLayouts.Count &&
        TableLayouts.Select(layout => layout.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == TableLayouts.Count &&
        (ActiveTableLayoutId is null || ActiveTableLayout is not null) && Receipt?.IsValid() == true;

    public string? Validate(PosCheckoutMetadata? metadata, bool requireValues, bool requireTable = false)
    {
        if (!IsValid()) return "POS checkout settings are invalid";
        if (metadata is null || metadata.Fields is null) return "Checkout details are required";
        if (metadata.TableNumber?.Length > 64) return "Table number must not exceed 64 characters";
        if (!TableTrackingEnabled && (!string.IsNullOrWhiteSpace(metadata.TableNumber) || metadata.TableId is not null || metadata.TableLayoutId is not null))
            return "Table tracking is disabled";
        if (TableTrackingEnabled)
        {
            var supplied = metadata.TableId is not null || metadata.TableLayoutId is not null || !string.IsNullOrWhiteSpace(metadata.TableNumber);
            if (supplied || requireTable || (requireValues && TableRequired))
            {
                if (ActiveTableLayout is not { Tables.Count: > 0 } layout)
                    return "Configure an active table layout in POS settings";
                if (metadata.TableLayoutId != layout.Id || !layout.Tables.Any(table => table.Id == metadata.TableId))
                    return "Select a table from the current layout";
            }
        }
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
    public Guid? TableLayoutId { get; set; }
    public Guid? TableId { get; set; }
}

[MemoryPackable]
public partial record PosCheckoutSettingsResponse
{
    public Guid TenantId { get; set; }
    public PosCheckoutSettings Settings { get; set; } = new();
}
