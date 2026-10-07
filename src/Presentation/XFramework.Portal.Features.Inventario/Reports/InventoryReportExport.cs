using System.Globalization;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Portal.Features.Inventario.Reports;

public sealed record InventoryReportExport(string Title, string TenantLabel, DateTime GeneratedAt,
    string Scope, List<InventoryExportSection> Sections, List<InventoryExportChart> Charts)
{
    public static InventoryReportExport Create(InventoryReportSnapshot snapshot, string tenantLabel, string scope)
    {
        static string N(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        static string D(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? "";
        InventoryExportSection Expiry(string heading, List<NearExpiryStockReportRow> rows) => new(heading,
            ["Product", "Lot", "Warehouse", "Location", "Available", "Expires UTC"],
            rows.Select(x => new[] { x.ProductName, x.LotNumber, x.WarehouseName ?? "", x.LocationName ?? "",
                N(x.AvailableQuantity), D(x.ExpiresAt) }).ToArray());
        return new("Inventory reports", tenantLabel, snapshot.GeneratedAtUtc, scope,
            [new("Stock summary (all matching positions)", ["Measure", "Quantity"],
                [["On hand", N(snapshot.OnHand)], ["Reserved", N(snapshot.Reserved)], ["Available", N(snapshot.Available)]]),
             new($"Stock positions ({snapshot.Positions.Count} of {snapshot.PositionCount})",
                ["Product", "Variant", "Warehouse", "Location", "Lot", "On hand", "Reserved", "Available"],
                snapshot.Positions.Select(x => new[] { x.ProductName, x.ProductVariationName ?? "Base product",
                    x.WarehouseName, x.LocationName, x.LotNumber ?? "", N(x.OnHandQuantity), N(x.ReservedQuantity), N(x.AvailableQuantity) }).ToArray()),
             new($"Movements ({snapshot.Movements.Count} of {snapshot.MovementCount})",
                ["Date UTC", "Product", "Warehouse", "Location", "Type", "Quantity", "Reference"],
                snapshot.Movements.Select(x => new[] { D(x.MovementDate), x.ProductName, x.WarehouseName,
                    x.LocationName, x.MovementType.ToString(), N(x.QuantityDelta), x.ReferenceType ?? "" }).ToArray()),
             Expiry(snapshot.ExpiryEnabled ? "Near expiry (up to 1,000 rows)" : "Near expiry unavailable (Traceability disabled)", snapshot.NearExpiry),
             Expiry(snapshot.ExpiryEnabled ? "Expired stock (up to 1,000 rows)" : "Expired stock unavailable (Traceability disabled)", snapshot.Expired)],
            [new("Current stock quantities", ["On hand", "Reserved", "Available"],
                [(double)snapshot.OnHand, (double)snapshot.Reserved, (double)snapshot.Available]),
             new("Movement quantities in selected period", ["Inbound", "Outbound"],
                [(double)snapshot.Inbound, (double)snapshot.Outbound])]);
    }
}

public sealed record InventoryExportSection(string Heading, string[] Columns, string[][] Rows);
public sealed record InventoryExportChart(string Title, string[] Labels, double[] Values);
