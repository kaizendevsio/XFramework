using System.Globalization;
using POS.Domain.Shared.Contracts.Responses;

namespace XFramework.Portal.Features.POS.Reports;

public sealed record PosReportExport(string Title, string TenantLabel, DateTime GeneratedAt,
    string Scope, List<PosExportSection> Sections, List<PosExportChart> Charts)
{
    public static PosReportExport Create(PosSalesReportResponse report, PosReportScope scope,
        string tenantLabel, Func<Guid, string> currencyLabel)
    {
        static string N(decimal n) => n.ToString("0.00", CultureInfo.InvariantCulture);
        var sections = new List<PosExportSection>
        {
            new("Sales and refunds (full filtered period, tax inclusive)",
                ["Currency", "Receipts", "Gross sales", "Refunds", "Net sales"],
                report.Currencies.Select(x => new[] { currencyLabel(x.CurrencyId), x.CompletedCount.ToString(),
                    N(x.Total), N(x.RefundTotal), N(x.NetTotal) }).ToArray()),
            new("Sale status (full filtered period)", ["Status", "Count"],
                report.Statuses.Select(x => new[] { x.Status.ToString(), x.Count.ToString() }).ToArray()),
            new($"Receipts: page {report.Page}, {report.Rows.Count} of {report.TotalCount} matching rows",
                ["Receipt", "Created UTC", "Register", "Currency", "Total", "Status"],
                report.Rows.Select(x => new[] { x.SaleNumber, x.CreatedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                    x.RegisterName, currencyLabel(x.CurrencyId), N(x.TotalAmount), x.Status.ToString() }).ToArray()),
            new($"Refunds: page {report.RefundPage}, {report.RefundRows.Count} of {report.RefundCount} matching rows",
                ["Return", "Refunded UTC", "Currency", "Method", "Amount"],
                report.RefundRows.Select(x => new[] { x.ReturnNumber, x.RefundedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    currencyLabel(x.CurrencyId), x.RefundMethod.ToString(), N(x.TotalRefundAmount) }).ToArray())
        };
        var charts = report.Currencies.Select(currency =>
        {
            var days = report.Timeline.Where(x => x.CurrencyId == currency.CurrencyId).ToList();
            return new PosExportChart($"Net sales - {currencyLabel(currency.CurrencyId)}",
                days.Select(x => x.DateUtc.ToString(report.ToUtcExclusive - report.FromUtc == TimeSpan.FromDays(1)
                    ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd", CultureInfo.InvariantCulture)).ToArray(),
                days.Select(x => (double)x.NetTotal).ToArray());
        }).ToList();
        return new("POS sales report", tenantLabel, report.GeneratedAtUtc,
            $"{report.FromUtc:yyyy-MM-dd} through {report.ToUtcExclusive.AddDays(-1):yyyy-MM-dd} UTC. Sales by completion date; refunds by successful refund date. " +
            $"Register: {scope.RegisterId?.ToString() ?? "All"}. Currency: {(scope.CurrencyId is { } c ? currencyLabel(c) : "All (separate totals)")}. " +
            $"Status: {scope.Status?.ToString() ?? "All"}. Payment: {scope.PaymentMethod?.ToString() ?? "All"}. " +
            $"Hour UTC: {scope.HourUtc?.ToString() ?? "All"}. Net sales deduct successful refunds, including refunds of older sales.", sections, charts);
    }
}

public sealed record PosExportSection(string Heading, string[] Columns, string[][] Rows);
public sealed record PosExportChart(string Title, string[] Labels, double[] Values);
