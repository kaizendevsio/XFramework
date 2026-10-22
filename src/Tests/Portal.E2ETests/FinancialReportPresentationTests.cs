using System.Text.Json;
using FluentAssertions;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Portal.Features.Finance.Services;

namespace Portal.E2ETests;

[TestFixture, Category("Area:PortalContract")]
public sealed class FinancialReportPresentationTests
{
    [Test]
    public void Daily_SeparateCurrencies_FillsOnlyMissingDaysWithZero()
    {
        var eur = Guid.NewGuid(); var usd = Guid.NewGuid();
        var from = new DateTime(2026, 9, 1);
        var snapshot = new WalletFinancialReportResponse { DailyActivity = [
            new() { CurrencyId = eur, Date = from, Credits = 17, Debits = 5 },
            new() { CurrencyId = usd, Date = from, Credits = 900, Debits = 100 }
        ] };
        var daily = FinancialReportPresentation.Daily(snapshot, eur, from, from.AddDays(2));
        daily.Should().HaveCount(3);
        daily[0].Should().Be(new FinancialReportDailyPoint("2026-09-01", 17, 5));
        daily[0].Net.Should().Be(12);
        daily[1].Credits.Should().Be(0);
        daily[2].Date.Should().Be("2026-09-03");
    }

    [Test]
    public void Export_SelectedCurrencyAndDay_PreservesSnapshotAndSeparateCreditDebitSeries()
    {
        var eur = Guid.NewGuid(); var usd = Guid.NewGuid();
        var from = new DateTime(2026, 9, 1);
        var snapshot = new WalletFinancialReportResponse {
            GeneratedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            Currencies = [new() { CurrencyId = eur, Currency = "EUR", Credits = 17, Debits = 5 }, new() { CurrencyId = usd, Currency = "USD", Credits = 900 }],
            DailyActivity = [new() { CurrencyId = eur, Date = from, Credits = 17, Debits = 5 }, new() { CurrencyId = usd, Date = from, Credits = 900 }]
        };
        var export = JsonSerializer.SerializeToElement(FinancialReportPresentation.Export(snapshot, "Test tenant", from, from.AddDays(2), eur.ToString(), "2026-09-01"));
        export.GetProperty("generatedAt").GetString().Should().Be("2026-10-01 12:00:00Z");
        export.GetProperty("charts").GetArrayLength().Should().Be(2);
        export.GetProperty("charts")[0].GetProperty("values")[0].GetDecimal().Should().Be(17);
        export.GetProperty("charts")[1].GetProperty("values")[0].GetDecimal().Should().Be(5);
        export.GetProperty("sections")[2].GetProperty("rows").GetArrayLength().Should().Be(1);
        export.GetRawText().Should().NotContain("USD").And.Contain("EUR");
        snapshot.Currencies.Should().HaveCount(2);
    }

    [Test]
    public void Currencies_MissingSelection_DoesNotBroadenToAllCurrencies()
    {
        var snapshot = new WalletFinancialReportResponse { Currencies = [new() { CurrencyId = Guid.NewGuid(), Currency = "EUR" }] };
        FinancialReportPresentation.Currencies(snapshot, Guid.NewGuid().ToString()).Should().BeEmpty();
    }

    [TestCase(null, true)]
    [TestCase("all", true)]
    [TestCase("unspecified", true)]
    [TestCase("invalid", false)]
    [TestCase("", false)]
    [TestCase("00000000-0000-0000-0000-000000000000", false)]
    public void ValidCurrency_RejectsMalformedFilters(string? value, bool valid) =>
        FinancialReportPresentation.ValidCurrency(value).Should().Be(valid);
}
