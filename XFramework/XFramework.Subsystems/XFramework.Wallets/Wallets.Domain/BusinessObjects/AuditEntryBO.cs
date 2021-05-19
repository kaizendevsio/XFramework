using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Wallets.Domain.DataTransferObjects;

namespace Wallets.Domain.BusinessObjects
{
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

        public TblAuditHistory ToAudit()
        {
            var audit = new TblAuditHistory();
            audit.TableName = TableName;
            audit.CreatedAt = DateTime.UtcNow;
            audit.KeyValues = JsonSerializer.Serialize(KeyValues);
            audit.QueryAction = QueryAction;
            audit.OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues);
            audit.NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues);
            return audit;
        }
    }
}
