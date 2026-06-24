using Messaging.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.Domain.Shared.Configurations;

public sealed class MessageThreadInviteConfiguration : IEntityTypeConfiguration<MessageThreadInvite>
{
    public void Configure(EntityTypeBuilder<MessageThreadInvite> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagethreadinvite_pk");
        entity.ToTable("MessageThreadInvite", "Messaging");
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
        entity.ToTable("MessagePin", "Messaging");
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
        entity.ToTable("MessageSaved", "Messaging");
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

public sealed class MessageReportConfiguration : IEntityTypeConfiguration<MessageReport>
{
    public void Configure(EntityTypeBuilder<MessageReport> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagereport_pk");
        entity.ToTable("MessageReport", "Messaging");
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

public sealed class MessageBlockConfiguration : IEntityTypeConfiguration<MessageBlock>
{
    public void Configure(EntityTypeBuilder<MessageBlock> entity)
    {
        entity.HasKey(e => e.Id).HasName("messageblock_pk");
        entity.ToTable("MessageBlock", "Messaging");
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
