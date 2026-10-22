using System.Security.Cryptography;
using System.Text.Json;

namespace XFramework.Inventario.Domain.Shared.Contracts;

[MemoryPackable]
public sealed partial record ProductVariationOption
{
    public Guid ProductVariationTypeId { get; init; }
    public string Value { get; init; } = "";
}

public static class ProductVariationCombination
{
    public const int MaxOptions = 8;
    public const int MaxValueLength = 100;
    public const decimal MaxPrice = 9999999999999999.99m;

    public static string? Validate(IReadOnlyList<ProductVariationOption>? options)
    {
        if (options is null || options.Count < 2 || options.Count > MaxOptions)
            return $"A combination requires between 2 and {MaxOptions} options.";
        if (options.Any(x => x is null || x.ProductVariationTypeId == Guid.Empty))
            return "Select a variation type for every option.";
        if (options.Select(x => x.ProductVariationTypeId).Distinct().Count() != options.Count)
            return "Each variation type can appear only once in a combination.";
        if (options.Any(x => string.IsNullOrWhiteSpace(x.Value) || x.Value.Trim().Length > MaxValueLength))
            return $"Each option needs a value of at most {MaxValueLength} characters.";
        return null;
    }

    public static List<ProductVariationOption> Normalize(IEnumerable<ProductVariationOption> options) =>
        options.OrderBy(x => x.ProductVariationTypeId)
            .Select(x => x with { Value = NormalizeValue(x.Value) }).ToList();

    // JSON preserves boundaries even when values contain separators; the bounded hash fits a B-tree index.
    public static string CreateKey(IEnumerable<ProductVariationOption> options) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            Normalize(options).Select(x => x with { Value = x.Value.ToUpperInvariant() }).ToList())));

    private static string NormalizeValue(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
