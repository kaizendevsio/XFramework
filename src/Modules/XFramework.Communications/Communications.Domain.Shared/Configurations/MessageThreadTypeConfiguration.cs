using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Communications.Domain.Shared.Contracts;

namespace Communications.Domain.Shared.Configurations;

public class MessageThreadTypeConfiguration : IEntityTypeConfiguration<MessageThreadType>
{
    public void Configure(EntityTypeBuilder<MessageThreadType> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagethreadentity_pk");

        entity.ToTable("MessageThreadType", "Communications");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");

        entity.HasOne(d => d.MessageType).WithMany(p => p.MessageThreadTypes)
            .HasForeignKey(d => d.MessageTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagethreadentity_messagetype_id_fk");
    }
}
