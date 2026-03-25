using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messaging.Domain.Shared.Contracts;

namespace Messaging.Domain.Shared.Configurations;

public class MessageReactionTypeConfiguration : IEntityTypeConfiguration<MessageReactionType>
{
    public void Configure(EntityTypeBuilder<MessageReactionType> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagereactionentity_pk");

        entity.ToTable("MessageReactionType", "Messaging");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Emoji).HasColumnType("character varying");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");
    }
}
