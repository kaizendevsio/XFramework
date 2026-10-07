using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;

namespace XFramework.Portal.Features.Inventario.Reports;

public sealed record InventoryReportScope(Guid TenantId, Guid? ProductId, Guid? WarehouseId,
    Guid? LocationId, DateTime FromUtc, DateTime ToUtc, int DaysAhead)
{
    public static InventoryReportScope Default(Guid tenantId) => new(tenantId, null, null, null,
        DateTime.UtcNow.Date.AddDays(-29), DateTime.UtcNow.Date.AddDays(1).AddTicks(-1), 30);

    public static InventoryReportScope? Parse(string uri, Guid? selectedTenant)
    {
        var query = QueryHelpers.ParseQuery(new Uri(uri).Query);
        if (query.Count == 0)
            return selectedTenant is { } id ? Default(id) : null;
        // A malformed saved filter must never broaden into an all-inventory report.
        if (!query.TryGetValue("tenant", out var tenantValue) || !Guid.TryParse(tenantValue, out var tenant) || tenant == Guid.Empty)
            throw new FormatException("This report link has no valid tenant. Open reports from the selected tenant.");
        var defaults = Default(tenant);
        Guid? Id(string key)
        {
            if (!query.ContainsKey(key)) return null;
            return Guid.TryParse(query[key], out var id) && id != Guid.Empty
                ? id : throw new FormatException("This report link contains an invalid filter.");
        }
        DateTime Date(string key, DateTime fallback)
        {
            if (!query.ContainsKey(key)) return fallback;
            return DateTime.TryParseExact(query[key], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                ? date : throw new FormatException("This report link contains an invalid date.");
        }
        var days = query.ContainsKey("expiry")
            ? int.TryParse(query["expiry"], out var parsed) ? parsed : 0 : 30;
        var from = Date("from", defaults.FromUtc);
        var to = Date("to", defaults.ToUtc.Date).AddDays(1).AddTicks(-1);
        if (days is < 1 or > 365 || from > to || to - from > TimeSpan.FromDays(366))
            throw new FormatException("Choose a date range of at most 366 days and an expiry window of 1-365 days.");
        return new(tenant, Id("product"), Id("warehouse"), Id("location"), from, to, days);
    }

    public string ShareUri(NavigationManager navigation) => navigation.GetUriWithQueryParameters(
        navigation.ToAbsoluteUri("inventario/reports").ToString(),
        new Dictionary<string, object?>
        {
            ["tenant"] = TenantId, ["product"] = ProductId, ["warehouse"] = WarehouseId,
            ["location"] = LocationId, ["from"] = FromUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = ToUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["expiry"] = DaysAhead
        });

    public GetInventoryReportSnapshotRequest Request() => new()
    {
        Metadata = new RequestMetadata
        {
            RequestedTenantId = TenantId, RequestId = Guid.NewGuid(), OperationName = "Portal.InventoryReports"
        },
        ProductId = ProductId, WarehouseId = WarehouseId, LocationId = LocationId,
        FromUtc = FromUtc, ToUtc = ToUtc, DaysAhead = DaysAhead
    };
}
