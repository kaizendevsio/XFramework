using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Enums;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Portal.Features.POS.Reports;

public sealed record PosReportScope(Guid TenantId, DateTime FromUtc, DateTime ToUtcExclusive,
    Guid? RegisterId = null, Guid? CurrencyId = null, PosSaleStatus? Status = null, int Page = 1,
    PosPaymentMethod? PaymentMethod = null, int? HourUtc = null, int RefundPage = 1)
{
    public static PosReportScope Period(Guid tenant, string period, DateTime now)
    {
        var today = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var from = period switch
        {
            "week" => today.AddDays(-((7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7)),
            "month" => new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => today
        };
        return new(tenant, from, today.AddDays(1));
    }

    public static PosReportScope? Parse(string uri, Guid? selectedTenant, bool overview)
    {
        var query = QueryHelpers.ParseQuery(new Uri(uri).Query);
        Guid? Id(string key)
        {
            if (!query.TryGetValue(key, out var value)) return null;
            return value.Count == 1 && Guid.TryParse(value, out var id) && id != Guid.Empty
                ? id : throw new FormatException("This report link contains an invalid tenant or filter.");
        }
        var tenant = Id("tenant") ?? (query.Count == 0 ? selectedTenant :
            throw new FormatException("This report link requires a tenant."));
        if (tenant is null || tenant == Guid.Empty) return null;
        var defaults = Period(tenant.Value, overview ? "today" : "month", DateTime.UtcNow);
        DateTime Date(string key, DateTime fallback)
        {
            if (!query.TryGetValue(key, out var value)) return fallback;
            return value.Count == 1 && DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                ? date : throw new FormatException("This report link contains an invalid date.");
        }
        var from = Date("from", defaults.FromUtc);
        var through = Date("to", defaults.ToUtcExclusive.AddDays(-1));
        if (through >= DateTime.MaxValue.Date || through < from || (through - from).TotalDays >= 366)
            throw new FormatException("Choose a date range of 1-366 days.");
        PosSaleStatus? status = null;
        if (query.TryGetValue("status", out var statusValue))
            status = statusValue.Count == 1 && Enum.TryParse<PosSaleStatus>(statusValue, out var parsed) && Enum.IsDefined(parsed)
                ? parsed : throw new FormatException("This report link contains an invalid status.");
        var page = 1;
        if (query.TryGetValue("page", out var pageValue) &&
            (pageValue.Count != 1 || !int.TryParse(pageValue, out page) || page is < 1 or > 100000))
            throw new FormatException("This report link contains an invalid page.");
        PosPaymentMethod? method = null;
        if (query.TryGetValue("payment", out var methodValue))
            method = methodValue.Count == 1 && Enum.TryParse<PosPaymentMethod>(methodValue, out var parsedMethod) && Enum.IsDefined(parsedMethod)
                ? parsedMethod : throw new FormatException("This report link contains an invalid payment method.");
        int? hour = null;
        if (query.TryGetValue("hour", out var hourValue))
            hour = hourValue.Count == 1 && int.TryParse(hourValue, out var parsedHour) && parsedHour is >= 0 and <= 23 && from == through
                ? parsedHour : throw new FormatException("This report link contains an invalid hourly scope.");
        var refundPage = 1;
        if (query.TryGetValue("refundPage", out var refundPageValue) &&
            (refundPageValue.Count != 1 || !int.TryParse(refundPageValue, out refundPage) || refundPage is < 1 or > 100000))
            throw new FormatException("This report link contains an invalid refund page.");
        return new(tenant.Value, from, through.AddDays(1), Id("register"), Id("currency"), status, page, method, hour, refundPage);
    }

    public string ShareUri(NavigationManager navigation, bool overview = false) => navigation.GetUriWithQueryParameters(
        navigation.ToAbsoluteUri(overview ? "pos/overview" : "pos/reports").ToString(),
        new Dictionary<string, object?>
        {
            ["tenant"] = TenantId, ["from"] = FromUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = ToUtcExclusive.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["register"] = RegisterId, ["currency"] = CurrencyId, ["status"] = Status?.ToString(), ["page"] = Page,
            ["payment"] = PaymentMethod?.ToString(), ["hour"] = HourUtc, ["refundPage"] = RefundPage
        });

    public GetPosSalesReportRequest Request() => new()
    {
        Metadata = new RequestMetadata { RequestedTenantId = TenantId, RequestId = Guid.NewGuid(), OperationName = "Portal.PosReports" },
        FromUtc = FromUtc, ToUtcExclusive = ToUtcExclusive, RegisterId = RegisterId,
        CurrencyId = CurrencyId, Status = Status, Page = Page, PageSize = 25,
        PaymentMethod = PaymentMethod, HourUtc = HourUtc, RefundPage = RefundPage
    };
}
