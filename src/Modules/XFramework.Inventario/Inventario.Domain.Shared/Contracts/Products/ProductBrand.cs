using System.Text.RegularExpressions;

namespace XFramework.Inventario.Domain.Shared.Contracts.Products;

public static class ProductBrand
{
    public const int MaxLength = 100;
    // Explicit Unicode whitespace keeps .NET and PostgreSQL normalization identical.
    public const string WhitespacePattern = "[\\u0009-\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]+";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Regex.Replace(value, WhitespacePattern, " ").Trim().ToUpperInvariant();
    }
}

