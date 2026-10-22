using System.Globalization;
using Wallets.Domain.Shared.Contracts.Responses;

namespace XFramework.Portal.Features.Finance.Services;

public static class FinancialReportPresentation
{
    public static string CurrencyKey(Guid? id) => id?.ToString() ?? "unspecified";

    public static bool ValidCurrency(string? value) => value is null or "all" or "unspecified" ||
        Guid.TryParse(value, out var id) && id != Guid.Empty;

    public static List<WalletFinancialCurrencySummary> Currencies(WalletFinancialReportResponse snapshot, string selected) =>
        snapshot.Currencies.Where(x => selected == "all" || CurrencyKey(x.CurrencyId) == selected).ToList();

    public static List<FinancialReportDailyPoint> Daily(WalletFinancialReportResponse snapshot, Guid? currencyId, DateTime from, DateTime through)
    {
        var points = snapshot.DailyActivity.Where(x => x.CurrencyId == currencyId).ToDictionary(x => x.Date.Date);
        return Enumerable.Range(0, (through.Date - from.Date).Days + 1).Select(offset =>
        {
            var date = from.Date.AddDays(offset);
            points.TryGetValue(date, out var row);
            return new FinancialReportDailyPoint(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row?.Credits ?? 0, row?.Debits ?? 0);
        }).ToList();
    }

    public static object Export(WalletFinancialReportResponse snapshot, string tenantLabel, DateTime from,
        DateTime through, string selectedCurrency, string? selectedDay)
    {
        var currencies = Currencies(snapshot, selectedCurrency);
        var detail = currencies.SelectMany(currency => Daily(snapshot, currency.CurrencyId, from, through)
            .Where(x => selectedDay is null || x.Date == selectedDay)
            .Select(x => new[] { currency.Currency, x.Date, Money(x.Credits), Money(x.Debits), Money(x.Net) })).ToArray();
        return new
        {
            title = "Financial Report", tenantLabel, generatedAt = snapshot.GeneratedAt.ToString("u"),
            scope = $"{from:yyyy-MM-dd} to {through:yyyy-MM-dd} UTC. " +
                $"Currencies: {string.Join(", ", currencies.Select(x => x.Currency))}. " +
                $"Daily detail: {selectedDay ?? "all days"}. Current balances; activity excludes holds and external counter-entries.",
            charts = currencies.SelectMany(currency =>
            {
                var daily = Daily(snapshot, currency.CurrencyId, from, through);
                return new[]
                {
                    new { title = $"Daily credits - {currency.Currency}", labels = daily.Select(x => x.Date).ToArray(), values = daily.Select(x => x.Credits).ToArray() },
                    new { title = $"Daily debits (including fees) - {currency.Currency}", labels = daily.Select(x => x.Date).ToArray(), values = daily.Select(x => x.Debits).ToArray() }
                };
            }).ToArray(),
            sections = new[]
            {
                new { heading = "Current balances", columns = new[] { "Currency", "Wallets", "Balance", "Available", "Debit hold", "Credit hold" }, rows = currencies.Select(x => new[] { x.Currency, x.WalletCount.ToString(), Money(x.Balance), Money(x.AvailableBalance), Money(x.DebitHold), Money(x.CreditHold) }).ToArray() },
                new { heading = "Posted activity", columns = new[] { "Currency", "Credits", "Debits incl. fees", "Entries" }, rows = currencies.Select(x => new[] { x.Currency, Money(x.Credits), Money(x.Debits), x.EntryCount.ToString() }).ToArray() },
                new { heading = "Daily detail (UTC)", columns = new[] { "Currency", "Date", "Credits", "Debits incl. fees", "Net" }, rows = detail }
            }
        };
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}
