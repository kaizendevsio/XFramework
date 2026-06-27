using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Communications.Domain.Shared.Contracts;

namespace Communications.Domain.Shared.Configurations;

public class MessageDeliveryTypeConfiguration : IEntityTypeConfiguration<MessageDeliveryType>
{
    public void Configure(EntityTypeBuilder<MessageDeliveryType> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagedeliveryentity_pk");

        entity.ToTable("MessageDeliveryType", "Communications");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");
    }
}
