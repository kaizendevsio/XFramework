using System.Globalization;

namespace Inventario.Api.Features.Setup;

internal static class SetupCurrency
{
    private static readonly HashSet<string> Codes = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(x => new RegionInfo(x.Name).ISOCurrencySymbol).ToHashSet(StringComparer.Ordinal);

    internal static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Codes.Contains(value.Trim().ToUpperInvariant());
}
