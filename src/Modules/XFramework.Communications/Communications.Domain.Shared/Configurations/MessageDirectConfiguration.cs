using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Communications.Domain.Shared.Contracts;

namespace Communications.Domain.Shared.Configurations;

public class MessageDirectConfiguration : IEntityTypeConfiguration<MessageDirect>
{
    public void Configure(EntityTypeBuilder<MessageDirect> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagedirect_pk");

        entity.ToTable("MessageDirect", "Communications");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.Intent).HasColumnType("character varying");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.Message).HasColumnType("character varying");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ReceivedAt).HasColumnName("RecievedAt");
        entity.Property(e => e.Subject).HasColumnType("character varying");
        entity.Property(e => e.TemplateKey).HasColumnType("character varying");
        entity.Property(e => e.TemplateType).HasColumnType("character varying");
        entity.Property(e => e.TemplateVariablesJson)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        entity.HasIndex(e => e.TemplateId)
            .HasDatabaseName("IX_MessageDirect_TemplateId");

        entity.HasIndex(e => new { e.TenantId, e.IdempotencyRequestId })
            .IsUnique()
            .HasFilter("\"IdempotencyRequestId\" IS NOT NULL")
            .HasDatabaseName("UX_MessageDirect_Tenant_IdempotencyRequest");

        entity.HasOne(d => d.ParentMessage).WithMany(p => p.InverseParentMessage)
            .HasForeignKey(d => d.ParentMessageId)
            .HasConstraintName("messagedirect_messagedirect_id_fk");

        entity.HasOne(d => d.Recipient).WithMany()
            .HasForeignKey(d => d.RecipientId)
            .HasConstraintName("messagedirect_identitycredential_2_id_fk");

        entity.HasOne(d => d.Sender).WithMany()
            .HasForeignKey(d => d.SenderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagedirect_identitycredential_id_fk");

        entity.HasOne(d => d.Type).WithMany(p => p.MessageDirects)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagedirect_messagetype_id_fk");
    }
}
