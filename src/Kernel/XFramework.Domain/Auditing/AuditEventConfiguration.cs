using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace XFramework.Domain.Auditing;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> entity)
    {
        entity.ToTable("event", "audit", table =>
            table.HasComment("Append-only, database-triggered audit trail."));

        entity.HasKey(item => item.EventId);
        entity.Property(item => item.EventId)
            .HasColumnName("event_id")
            .UseIdentityAlwaysColumn();
        entity.Property(item => item.EventUuid)
            .HasColumnName("event_uuid")
            .HasDefaultValueSql("gen_random_uuid()");
        entity.Property(item => item.RecordedAt)
            .HasColumnName("recorded_at")
            .HasDefaultValueSql("clock_timestamp()");
        entity.Property(item => item.EventKind).HasColumnName("event_kind").HasMaxLength(32);
        entity.Property(item => item.Operation).HasColumnName("operation").HasMaxLength(32);
        entity.Property(item => item.TransactionId).HasColumnName("transaction_id");
        entity.Property(item => item.TransactionOrdinal).HasColumnName("transaction_ordinal");
        entity.Property(item => item.ChangeSetId).HasColumnName("change_set_id");
        entity.Property(item => item.DatabaseUser).HasColumnName("database_user").HasMaxLength(128);
        entity.Property(item => item.ServiceName).HasColumnName("service_name").HasMaxLength(200);
        entity.Property(item => item.Environment).HasColumnName("environment").HasMaxLength(64);
        entity.Property(item => item.InstanceId).HasColumnName("instance_id").HasMaxLength(200);
        entity.Property(item => item.ActorKind).HasColumnName("actor_kind").HasMaxLength(32);
        entity.Property(item => item.ActorCredentialId).HasColumnName("actor_credential_id");
        entity.Property(item => item.ActorIdentityId).HasColumnName("actor_identity_id");
        entity.Property(item => item.ActorTenantId).HasColumnName("actor_tenant_id");
        entity.Property(item => item.EffectiveTenantId).HasColumnName("effective_tenant_id");
        entity.Property(item => item.SubjectTenantIdBefore).HasColumnName("subject_tenant_id_before");
        entity.Property(item => item.SubjectTenantIdAfter).HasColumnName("subject_tenant_id_after");
        entity.Property(item => item.SessionId).HasColumnName("session_id");
        entity.Property(item => item.CorrelationId).HasColumnName("correlation_id");
        entity.Property(item => item.TraceId).HasColumnName("trace_id").HasMaxLength(32);
        entity.Property(item => item.SpanId).HasColumnName("span_id").HasMaxLength(16);
        entity.Property(item => item.OperationName).HasColumnName("operation_name").HasMaxLength(300);
        entity.Property(item => item.ClientIp).HasColumnName("client_ip").HasMaxLength(64);
        entity.Property(item => item.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        entity.Property(item => item.SchemaName).HasColumnName("schema_name").HasMaxLength(128);
        entity.Property(item => item.TableName).HasColumnName("table_name").HasMaxLength(128);
        entity.Property(item => item.EntityKey).HasColumnName("entity_key").HasColumnType("jsonb");
        entity.Property(item => item.ChangedFields).HasColumnName("changed_fields").HasColumnType("text[]");
        entity.Property(item => item.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
        entity.Property(item => item.NewValues).HasColumnName("new_values").HasColumnType("jsonb");

        entity.HasIndex(item => item.EventUuid).IsUnique();
        entity.HasIndex(item => item.EntityKey).HasMethod("gin");
        entity.HasIndex(item => new { item.SubjectTenantIdAfter, item.EventId });
        entity.HasIndex(item => new { item.SubjectTenantIdBefore, item.EventId });
        entity.HasIndex(item => new { item.SchemaName, item.TableName, item.EventId });
        entity.HasIndex(item => new { item.ActorCredentialId, item.EventId });
        entity.HasIndex(item => item.CorrelationId);
        entity.HasIndex(item => item.ChangeSetId);
        entity.HasIndex(item => item.RecordedAt)
            .HasMethod("brin");
    }
}
