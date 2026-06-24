using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts;

namespace Messaging.Domain.Shared.Configurations;

public class MessageThreadMemberConfiguration : IEntityTypeConfiguration<MessageThreadMember>
{
    public void Configure(EntityTypeBuilder<MessageThreadMember> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagethreadmember_pk");

        entity.ToTable("MessageThreadMember", "Messaging");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Alias).HasColumnType("character varying");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Description).HasColumnType("character varying");
        entity.Property(e => e.Emoji).HasColumnType("character varying");
        entity.Property(e => e.Role)
            .HasColumnType("character varying")
            .HasDefaultValue(MessageThreadMemberRoles.Member);

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageThreadId, e.CredentialId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageThreadMember_Thread_Credential_Active");

        entity.HasIndex(e => new { e.CredentialId, e.MessageThreadId })
            .HasDatabaseName("IX_MessageThreadMember_Credential_Thread");

        entity.HasOne(d => d.Group).WithMany(p => p.MessageThreadMembers)
            .HasForeignKey(d => d.GroupId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagethreadmember_messagethreadmembergroup_id_fk");

        entity.HasOne(d => d.Credential).WithMany()
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagethreadmember_identitycredential_id_fk");

        entity.HasOne(d => d.MessageThread).WithMany(p => p.MessageThreadMembers)
            .HasForeignKey(d => d.MessageThreadId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagethreadmember_messagethread_id_fk");
    }
}
