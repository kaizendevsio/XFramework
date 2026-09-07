using MemoryPack;
using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
namespace Audit.Domain.Shared;

[MemoryPackable]
public partial record SearchAuditEventsRequest : RequestBase, IBoltRequest<SearchAuditEventsRequest, QueryResponse<AuditPage>>
{
    public DateTimeOffset From { get; set; } = DateTimeOffset.UtcNow.AddDays(-7);
    public DateTimeOffset To { get; set; } = DateTimeOffset.UtcNow;
    public int StartIndex { get; set; }
    public int Count { get; set; } = 25;
    public string? Schema { get; set; }
    public string? Table { get; set; }
    public string? Service { get; set; }
    public string? Action { get; set; }
    public string? ActorKind { get; set; }
    public Guid? ActorId { get; set; }
    public string? EntityKey { get; set; }
    public string? ChangedField { get; set; }
    public Guid? CorrelationId { get; set; }
    public long? TransactionId { get; set; }
    public long? AnchorEventId { get; set; }
    public string? RelatedMode { get; set; }
    public bool OldestFirst { get; set; }
    public List<AuditFilter> Filters { get; set; } = [];
}
[MemoryPackable]
public partial record AuditFilter
{
    public string Field { get; set; } = "";
    public string Operator { get; set; } = "Contains";
    public string? Value { get; set; }
}
[MemoryPackable]
public partial record GetAuditEventRequest : RequestBase, IBoltRequest<GetAuditEventRequest, QueryResponse<AuditDetail>>
{
    public long EventId { get; set; }
}
[MemoryPackable]
public partial record AuditPage
{
    public List<AuditSummary> Items { get; set; } = [];
    public int TotalItemCount { get; set; }
}
[MemoryPackable]
public partial record AuditSummary
{
    public string? ActorName { get; set; }
    public long EventId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Action { get; set; } = "";
    public string ActorKind { get; set; } = "";
    public Guid? ActorId { get; set; }
    public string Service { get; set; } = "";
    public string Schema { get; set; } = "";
    public string Table { get; set; } = "";
    public string EntityKey { get; set; } = "{}";
    public string ChangedFields { get; set; } = "";
}
[MemoryPackable]
public partial record AuditDetail
{
    public AuditSummary Summary { get; set; } = new();
    public Guid EventUuid { get; set; }
    public long TransactionId { get; set; }
    public int TransactionOrdinal { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? ChangeSetId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string Notice { get; set; } = "";
    public List<AuditFieldChange> Fields { get; set; } = [];
}
[MemoryPackable]
public partial record AuditFieldChange
{
    public string Field { get; set; } = "";
    public string Before { get; set; } = "";
    public string After { get; set; } = "";
    public bool Changed { get; set; }
}
