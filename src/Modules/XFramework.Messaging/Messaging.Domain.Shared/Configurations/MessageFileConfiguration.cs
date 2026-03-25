using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messaging.Domain.Shared.Contracts;

namespace Messaging.Domain.Shared.Configurations;

public class MessageFileConfiguration : IEntityTypeConfiguration<MessageFile>
{
    public void Configure(EntityTypeBuilder<MessageFile> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagefiles_pk");

        entity.ToTable("MessageFiles", "Messaging");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasOne(d => d.Message).WithMany()
            .HasForeignKey(d => d.MessageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagefiles_message_id_fk");

        entity.HasOne(d => d.Storage).WithMany()
            .HasForeignKey(d => d.StorageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagefiles_storagefile_id_fk");
    }
}
