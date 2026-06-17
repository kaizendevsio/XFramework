using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messaging.Domain.Shared.Contracts;

namespace Messaging.Domain.Shared.Configurations;

public class MessageDeliveryConfiguration : IEntityTypeConfiguration<MessageDelivery>
{
    public void Configure(EntityTypeBuilder<MessageDelivery> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagedelivery_pk");

        entity.ToTable("MessageDelivery", "Messaging");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageThreadMemberId, e.MessageId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageDelivery_Member_Message_Active");

        entity.HasIndex(e => new { e.MessageId, e.TypeId })
            .HasDatabaseName("IX_MessageDelivery_Message_Type");

        entity.HasOne(d => d.Type).WithMany(p => p.MessageDeliveries)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagedelivery_messagedeliveryentity_id_fk");

        entity.HasOne(d => d.Message).WithMany(p => p.MessageDeliveries)
            .HasForeignKey(d => d.MessageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagedelivery_message_id_fk");

        entity.HasOne(d => d.MessageThreadMember).WithMany(p => p.MessageDeliveries)
            .HasForeignKey(d => d.MessageThreadMemberId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagedelivery_messagethreadmember_id_fk");
    }
}
