using Communications.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Communications.Domain.Shared.Configurations;

public sealed class MessageThreadInviteConfiguration : IEntityTypeConfiguration<MessageThreadInvite>
{
    public void Configure(EntityTypeBuilder<MessageThreadInvite> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagethreadinvite_pk");
        entity.ToTable("MessageThreadInvite", "Communications");
        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageThreadId, e.InvitedCredentialId, e.Status })
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("IX_MessageThreadInvite_Thread_Credential_Status");
    }
}

public sealed class MessagePinConfiguration : IEntityTypeConfiguration<MessagePin>
{
    public void Configure(EntityTypeBuilder<MessagePin> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagepin_pk");
        entity.ToTable("MessagePin", "Communications");
        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageThreadId, e.MessageId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessagePin_Thread_Message_Active");
    }
}

public sealed class MessageSavedConfiguration : IEntityTypeConfiguration<MessageSaved>
{
    public void Configure(EntityTypeBuilder<MessageSaved> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagesaved_pk");
        entity.ToTable("MessageSaved", "Communications");
        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageId, e.MessageThreadMemberId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageSaved_Message_Member_Active");
    }
}

public sealed class MessageHiddenConfiguration : IEntityTypeConfiguration<MessageHidden>
{
    public void Configure(EntityTypeBuilder<MessageHidden> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagehidden_pk");
        entity.ToTable("MessageHidden", "Communications");
        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageId, e.MessageThreadMemberId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageHidden_Message_Member_Active");
    }
}

public sealed class MessageReportConfiguration : IEntityTypeConfiguration<MessageReport>
{
    public void Configure(EntityTypeBuilder<MessageReport> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagereport_pk");
        entity.ToTable("MessageReport", "Communications");
        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Reason).HasColumnType("character varying");
        entity.Property(e => e.Details).HasColumnType("character varying");

        entity.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_MessageReport_Tenant_Status_CreatedAt");
    }
}

public sealed class MessageReportAuditConfiguration : IEntityTypeConfiguration<MessageReportAudit>
{
    public void Configure(EntityTypeBuilder<MessageReportAudit> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagereportaudit_pk");
        entity.ToTable("MessageReportAudit", "Communications");
        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Action).HasColumnType("character varying");
        entity.Property(e => e.Note).HasColumnType("character varying");

        entity.HasIndex(e => new { e.ReportId, e.CreatedAt })
            .HasDatabaseName("IX_MessageReportAudit_Report_CreatedAt");
    }
}

public sealed class MessageModerationRuleConfiguration : IEntityTypeConfiguration<MessageModerationRule>
{
    public void Configure(EntityTypeBuilder<MessageModerationRule> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagemoderationrule_pk");
        entity.ToTable("MessageModerationRule", "Communications");
        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");
        entity.Property(e => e.MatchType).HasColumnType("character varying");
        entity.Property(e => e.Pattern).HasColumnType("character varying");
        entity.Property(e => e.Action).HasColumnType("character varying");
        entity.Property(e => e.Description).HasColumnType("character varying");

        entity.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageModerationRule_Tenant_Name_Active");
    }
}

public sealed class MessageBlockConfiguration : IEntityTypeConfiguration<MessageBlock>
{
    public void Configure(EntityTypeBuilder<MessageBlock> entity)
    {
        entity.HasKey(e => e.Id).HasName("messageblock_pk");
        entity.ToTable("MessageBlock", "Communications");
        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.BlockerCredentialId, e.BlockedCredentialId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageBlock_Blocker_Blocked_Active");
    }
}
