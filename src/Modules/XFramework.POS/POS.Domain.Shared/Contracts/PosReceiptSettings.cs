namespace POS.Domain.Shared.Contracts;

[MemoryPackable]
public partial record PosReceiptSettings
{
    public const int MaxLogoBytes = 128 * 1024;
    public int PaperWidthMm { get; set; } = 80;
    public PosReceiptTemplate Template { get; set; } = PosReceiptTemplate.Classic;
    public string MerchantName { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string Footer { get; set; } = "Thank you for your purchase.";
    public string? LogoDataUrl { get; set; }
    public List<PosReceiptSection> SectionOrder { get; set; } = [.. Enum.GetValues<PosReceiptSection>()];
    public List<PosReceiptSection> HiddenSections { get; set; } = [];

    public static bool IsRequired(PosReceiptSection section) =>
        section is PosReceiptSection.Sale or PosReceiptSection.Items or PosReceiptSection.Totals;

    public bool IsValid() => PaperWidthMm is 58 or 80 && Enum.IsDefined(Template) &&
        MerchantName is { Length: <= 100 } && Header is { Length: <= 500 } && Footer is { Length: <= 500 } &&
        SectionOrder is { Count: 6 } && SectionOrder.All(Enum.IsDefined) && SectionOrder.Distinct().Count() == 6 &&
        HiddenSections is { Count: <= 3 } && HiddenSections.All(x => Enum.IsDefined(x) && !IsRequired(x)) &&
        HiddenSections.Distinct().Count() == HiddenSections.Count && IsValidLogo(LogoDataUrl);

    public PosReceiptSettings DeepClone() => this with
    {
        SectionOrder = [.. SectionOrder], HiddenSections = [.. HiddenSections]
    };

    public static bool IsValidLogo(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        if (value.Length > 175_000) return false;
        const string png = "data:image/png;base64,";
        const string jpeg = "data:image/jpeg;base64,";
        var isPng = value.StartsWith(png, StringComparison.Ordinal);
        if (!isPng && !value.StartsWith(jpeg, StringComparison.Ordinal)) return false;
        var bytes = new byte[MaxLogoBytes];
        if (!Convert.TryFromBase64String(value[(isPng ? png.Length : jpeg.Length)..], bytes, out var count)) return false;
        return isPng
            ? count >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : count >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[count - 2] == 255 && bytes[count - 1] == 217;
    }
}
