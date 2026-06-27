using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Communications.Domain.Shared.Contracts;

namespace Communications.Domain.Shared.Configurations;

public class MessageFileConfiguration : IEntityTypeConfiguration<MessageFile>
{
    public void Configure(EntityTypeBuilder<MessageFile> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagefiles_pk");

        entity.ToTable("MessageFiles", "Communications");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageId, e.StorageId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageFiles_Message_Storage_Active");

        entity.HasIndex(e => new { e.TenantId, e.MessageId, e.CreatedAt })
            .HasDatabaseName("IX_MessageFiles_Tenant_Message_Created");

        entity.HasOne(d => d.Message).WithMany(p => p.MessageFiles)
            .HasForeignKey(d => d.MessageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagefiles_message_id_fk");

        entity.HasOne(d => d.Storage).WithMany()
            .HasForeignKey(d => d.StorageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagefiles_storagefile_id_fk");
    }
}
