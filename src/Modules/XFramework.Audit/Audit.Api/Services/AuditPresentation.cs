using System.Text.Json;
using System.Text.Json.Nodes;
using XFramework.Domain.Auditing;

namespace Audit.Api.Services;

public static class AuditPresentation
{
    // Operational schemas permit field inspection. Identity, communications content,
    // infrastructure, and unknown sources remain metadata-only pending a disclosure review.
    private static readonly HashSet<string> PayloadTables = new(StringComparer.Ordinal)
    {
        "Inventario.Product", "Inventario.ProductCategory", "Inventario.ProductVariation", "Inventario.ProductVariationType",
        "Inventario.ProductTransaction", "Inventario.InventoryLocation", "Inventario.InventoryLot",
        "Inventario.InventoryMovement", "Inventario.InventoryReorderRule", "Inventario.StockBalance", "Inventario.Warehouse",
        "Inventario.Reservation", "Inventario.ReservationAllocation", "Inventario.PurchaseOrder", "Inventario.PurchaseOrderLine",
        "Inventario.ReceivingDocument", "Inventario.ReceivingLine",
        "Wallet.Wallet", "Wallet.WalletType", "Wallet.WalletBalanceSnapshot", "Wallet.WalletLedgerEntry",
        "Wallet.WalletTransaction", "Wallet.WalletTransactionLineItem", "Wallet.WalletTransfer",
        "POS.PosRegister", "POS.PosCart", "POS.PosCartLine", "POS.PosSale", "POS.PosSaleLine", "POS.PosReturn", "POS.PosReturnLine",
        "Attendance.AttendanceContext", "Attendance.AttendancePolicy", "Attendance.AttendanceRecord",
        "Attendance.AttendanceSession", "Attendance.AttendanceEvent", "Attendance.AttendanceAdjustment",
        "Storage.StorageFile", "Storage.StorageFileType"
    };

    public static AuditDetail ToDetail(AuditEvent e, Guid tenant)
    {
        var beforeRestricted = e.SubjectTenantIdBefore is Guid before && before != tenant;
        var afterRestricted = e.SubjectTenantIdAfter is Guid after && after != tenant;
        var payloadAllowed = PayloadTables.Contains(e.SchemaName + "." + e.TableName);
        var result = new AuditDetail
        {
            Summary = new AuditSummary
            {
                EventId = e.EventId, RecordedAt = e.RecordedAt, Action = e.EventKind,
                ActorKind = e.ActorKind, ActorId = e.ActorTenantId == tenant ? e.ActorCredentialId : null,
                Service = e.ServiceName ?? "Unknown", Schema = e.SchemaName, Table = e.TableName, EntityKey = SafeKey(e.EntityKey)
            },
            EventUuid = e.EventUuid, TransactionId = e.TransactionId, TransactionOrdinal = e.TransactionOrdinal,
            CorrelationId = e.CorrelationId, ChangeSetId = e.ChangeSetId, TraceId = e.TraceId, SpanId = e.SpanId,
            Notice = !payloadAllowed ? "This source exposes metadata only; its payload disclosure policy has not been approved."
                : beforeRestricted || afterRestricted ? "Other tenant data restricted." : "Secrets are redacted; binary values were omitted at capture."
        };
        if (!payloadAllowed) return result;
        result.Before = beforeRestricted ? null : Sanitize(e.OldValues);
        result.After = afterRestricted ? null : Sanitize(e.NewValues);
        var oldFields = Parse(result.Before);
        var newFields = Parse(result.After);
        foreach (var key in oldFields.Keys.Union(newFields.Keys).Order(StringComparer.Ordinal))
        {
            var oldValue = oldFields.GetValueOrDefault(key, "Not present");
            var newValue = newFields.GetValueOrDefault(key, "Not present");
            result.Fields.Add(new AuditFieldChange
            {
                Field = key, Before = beforeRestricted ? "Restricted" : oldValue,
                After = afterRestricted ? "Restricted" : newValue,
                Changed = !beforeRestricted && !afterRestricted && e.ChangedFields.Contains(key)
            });
        }
        return result;
    }

    private static Dictionary<string, string> Parse(string? json)
    {
        if (json is null) return [];
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText());
    }

    public static string SafeKey(string json)
    {
        var obj = JsonNode.Parse(json) as JsonObject;
        if (obj is null) return "{}";
        foreach (var key in obj.Select(p => p.Key).ToArray())
            if (!string.Equals(key, "Id", StringComparison.OrdinalIgnoreCase))
                obj[key] = "[RESTRICTED]";
        return obj.ToJsonString();
    }

    private static string? Sanitize(string? json)
    {
        if (json is null) return null;
        var node = JsonNode.Parse(json);
        Redact(node);
        return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(p => p.Key).ToArray())
            {
                var lower = key.ToLowerInvariant();
                if (lower.Contains("password") || lower.Contains("secret") || lower.Contains("token")
                    || lower.Contains("apikey") || lower.Contains("api_key") || lower.Contains("privatekey")
                    || lower.Contains("private_key") || lower is "otp" or "pin")
                    obj[key] = "[REDACTED]";
                else Redact(obj[key]);
            }
        }
        else if (node is JsonArray array)
            foreach (var child in array) Redact(child);
    }
}
