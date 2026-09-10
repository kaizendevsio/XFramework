using System.Globalization;

namespace Portal.E2ETests;

internal sealed record PosCashierE2ESettings
{
    public string BaseUrl { get; init; } = "http://127.0.0.1:5267";
    public string? StorageStatePath { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public required string TenantId { get; init; }
    public required string RegisterName { get; init; }
    public required string Currency { get; init; }
    public required string ProductName { get; init; }
    public required string ProductSku { get; init; }
    public required string VariantName { get; init; }
    public required string VariantQuery { get; init; }
    public required string VariantSku { get; init; }
    public int VariantSkuMatchCount { get; init; } = 2;
    public int ScrollItemCount { get; init; } = 3;
    public bool IgnoreHttpsErrors { get; init; }

    public static PosCashierE2ESettings Load()
    {
        var baseUrl = Optional("BASE_URL") ?? "http://127.0.0.1:5267";
        Assert.That(Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                    && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo),
            Is.True, "POS_E2E_BASE_URL must be an absolute HTTP(S) URL without embedded credentials.");

        var storageState = Optional("STORAGE_STATE");
        if (storageState is not null)
        {
            storageState = Path.GetFullPath(storageState);
            Assert.That(File.Exists(storageState), Is.True, "POS_E2E_STORAGE_STATE does not exist.");
        }
        else
        {
            Required("USERNAME");
            Required("PASSWORD");
        }

        var tenantId = Required("TENANT_ID");
        Assert.That(Guid.TryParse(tenantId, out var id) && id != Guid.Empty, Is.True,
            "POS_E2E_TENANT_ID must identify an existing authorized tenant.");

        var settings = new PosCashierE2ESettings
        {
            BaseUrl = baseUrl.TrimEnd('/'),
            StorageStatePath = storageState,
            Username = Optional("USERNAME"),
            Password = Environment.GetEnvironmentVariable("POS_E2E_PASSWORD"),
            TenantId = tenantId,
            RegisterName = Required("REGISTER_NAME"),
            Currency = Required("CURRENCY"),
            ProductName = Required("PRODUCT_NAME"),
            ProductSku = Required("PRODUCT_SKU"),
            VariantName = Required("VARIANT_NAME"),
            VariantQuery = Required("VARIANT_QUERY"),
            VariantSku = Required("VARIANT_SKU"),
            IgnoreHttpsErrors = Optional("IGNORE_HTTPS_ERRORS") == "1"
        };
        Assert.That(settings.ProductName, Is.Not.EqualTo(settings.VariantName),
            "Configure distinct base and variant display names.");
        Assert.That(settings.ProductSku, Is.Not.EqualTo(settings.VariantSku).IgnoreCase,
            "The uniquely scanned base-product SKU must differ from the variant lookup SKU.");

        if (Optional("VARIANT_SKU_MATCH_COUNT") is { } matches)
        {
            Assert.That(int.TryParse(matches, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                        && parsed is >= 1 and <= 24, Is.True,
                "POS_E2E_VARIANT_SKU_MATCH_COUNT must be between 1 and 24 (default 2).");
            settings = settings with { VariantSkuMatchCount = parsed };
        }

        if (Optional("SCROLL_ITEM_COUNT") is { } count)
        {
            Assert.That(int.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                        && parsed is >= 2 and <= 24, Is.True,
                "POS_E2E_SCROLL_ITEM_COUNT must be between 2 and 24 (default 3).");
            settings = settings with { ScrollItemCount = parsed };
        }

        return settings;
    }

    public static string? Optional(string suffix) =>
        Environment.GetEnvironmentVariable($"POS_E2E_{suffix}") is { } value
        && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static string Required(string suffix) => Optional(suffix)
        ?? throw new AssertionException($"Set POS_E2E_{suffix} before running POS browser tests. "
            + "See src/Tests/Portal.E2ETests/POS-E2E.md. Tests never create or guess fixture data.");
}
