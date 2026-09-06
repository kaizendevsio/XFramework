namespace XFramework.Domain.Auditing;

/// <summary>
/// Immutable database audit event. Rows are written by PostgreSQL triggers, not application code.
/// </summary>
public sealed class AuditEvent
{
    public long EventId { get; private set; }
    public Guid EventUuid { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public string EventKind { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public long TransactionId { get; private set; }
    public int TransactionOrdinal { get; private set; }
    public Guid? ChangeSetId { get; private set; }
    public string DatabaseUser { get; private set; } = string.Empty;
    public string? ServiceName { get; private set; }
    public string? Environment { get; private set; }
    public string? InstanceId { get; private set; }
    public string ActorKind { get; private set; } = string.Empty;
    public Guid? ActorCredentialId { get; private set; }
    public Guid? ActorIdentityId { get; private set; }
    public Guid? ActorTenantId { get; private set; }
    public Guid? EffectiveTenantId { get; private set; }
    public Guid? SubjectTenantIdBefore { get; private set; }
    public Guid? SubjectTenantIdAfter { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public string? TraceId { get; private set; }
    public string? SpanId { get; private set; }
    public string? OperationName { get; private set; }
    public string? ClientIp { get; private set; }
    public string? UserAgent { get; private set; }
    public string SchemaName { get; private set; } = string.Empty;
    public string TableName { get; private set; } = string.Empty;
    public string EntityKey { get; private set; } = "{}";
    public string[] ChangedFields { get; private set; } = [];
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
}
