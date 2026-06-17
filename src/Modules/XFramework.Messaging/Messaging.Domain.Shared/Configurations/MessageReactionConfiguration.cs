using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messaging.Domain.Shared.Contracts;

namespace Messaging.Domain.Shared.Configurations;

public class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagereaction_pk");

        entity.ToTable("MessageReaction", "Messaging");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageId, e.TypeId, e.MessageThreadMemberId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageReaction_Message_Type_Member_Active");

        entity.HasIndex(e => new { e.MessageThreadMemberId, e.MessageId })
            .HasDatabaseName("IX_MessageReaction_Member_Message");

        entity.HasOne(d => d.Type).WithMany(p => p.MessageReactions)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagereaction_messagereactionentity_id_fk");

        entity.HasOne(d => d.Message).WithMany(p => p.MessageReactions)
            .HasForeignKey(d => d.MessageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagereaction_message_id_fk");

        entity.HasOne(d => d.MessageThreadMember).WithMany()
            .HasForeignKey(d => d.MessageThreadMemberId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagereaction_messagethreadmember_id_fk");
    }
}
