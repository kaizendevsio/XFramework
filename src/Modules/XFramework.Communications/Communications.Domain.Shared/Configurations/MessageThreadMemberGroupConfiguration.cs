using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Communications.Domain.Shared.Contracts;

namespace Communications.Domain.Shared.Configurations;

public class MessageThreadMemberGroupConfiguration : IEntityTypeConfiguration<MessageThreadMemberGroup>
{
    public void Configure(EntityTypeBuilder<MessageThreadMemberGroup> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagethreadmembergroup_pk");

        entity.ToTable("MessageThreadMemberGroup", "Communications");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Alias).HasColumnType("character varying");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Description).HasColumnType("character varying");
        entity.Property(e => e.Emoji).HasColumnType("character varying");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasOne(d => d.MessageThread).WithMany(p => p.MessageThreadMemberGroups)
            .HasForeignKey(d => d.MessageThreadId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagethreadmembergroup_messagethread_id_fk");
    }
}
