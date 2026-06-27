using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Communications.Domain.Shared.Contracts;

namespace Communications.Domain.Shared.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> entity)
    {
        entity.HasKey(e => e.Id).HasName("message_pk");

        entity.ToTable("Message", "Communications");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.MentionedCredentialIdsJson)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");
        entity.Property(e => e.TemplateKey).HasColumnType("character varying");
        entity.Property(e => e.TemplateType).HasColumnType("character varying");
        entity.Property(e => e.TemplateVariablesJson)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");
        entity.Property(e => e.Text).HasColumnType("character varying");

        entity.HasIndex(e => new { e.MessageThreadId, e.CreatedAt, e.Id })
            .HasDatabaseName("IX_Message_Thread_CreatedAt_Id");

        entity.HasIndex(e => new { e.MessageThreadMemberId, e.CreatedAt })
            .HasDatabaseName("IX_Message_Member_CreatedAt");

        entity.HasIndex(e => e.TemplateId)
            .HasDatabaseName("IX_Message_TemplateId");

        entity.HasOne(d => d.MessageThread).WithMany(p => p.Messages)
            .HasForeignKey(d => d.MessageThreadId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("message_messagethread_id_fk");

        entity.HasOne(d => d.MessageThreadMember).WithMany(p => p.Messages)
            .HasForeignKey(d => d.MessageThreadMemberId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("message_messagethreadmember_id_fk");

        entity.HasOne(d => d.ParentMessage).WithMany(p => p.Replies)
            .HasForeignKey(d => d.ParentMessageId)
            .HasConstraintName("message_parent_message_id_fk");
    }
}
