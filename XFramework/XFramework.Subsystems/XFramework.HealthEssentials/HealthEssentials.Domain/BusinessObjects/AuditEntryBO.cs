using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using XFramework.Domain.Generic.Contracts;
using XFramework.Integration.Converters;

namespace HealthEssentials.Domain.BusinessObjects;

public class AuditEntryBO
{
    public AuditEntryBO(EntityEntry entry)
    {
        Entry = entry;
    }

    public EntityEntry Entry { get; }
    public string TableName { get; set; }
    public string QueryAction { get; set; }
    public Dictionary<string, object> KeyValues { get; } = new Dictionary<string, object>();
    public Dictionary<string, object> OldValues { get; } = new Dictionary<string, object>();
    public Dictionary<string, object> NewValues { get; } = new Dictionary<string, object>();
    public List<PropertyEntry> TemporaryProperties { get; } = new List<PropertyEntry>();

    public bool HasTemporaryProperties => TemporaryProperties.Any();

    public AuditHistory ToAudit()
    {
       var jsonSerializerOptions = new JsonSerializerOptions();
       jsonSerializerOptions.Converters.Add(new DateOnlyConverter());
        
        var audit = new AuditHistory();
        audit.TableName = TableName;
        audit.CreatedAt = DateTime.UtcNow;
        audit.KeyValues = JsonSerializer.Serialize(KeyValues, jsonSerializerOptions);
        audit.QueryAction = QueryAction;
        audit.OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues, jsonSerializerOptions);
        audit.NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues, jsonSerializerOptions);
        return audit;
    }
}