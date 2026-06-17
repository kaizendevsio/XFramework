using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Messaging.Domain.Shared.Contracts;

namespace Messaging.Domain.Shared.Configurations;

public class MessageThreadMemberRoleConfiguration : IEntityTypeConfiguration<MessageThreadMemberRole>
{
    public void Configure(EntityTypeBuilder<MessageThreadMemberRole> entity)
    {
        entity.HasKey(e => e.Id).HasName("messagethreadmemberrole_pk");

        entity.ToTable("MessageThreadMemberRole", "Messaging");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.MessageThreadMemberId, e.RoleId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_MessageThreadMemberRole_Member_Role_Active");

        entity.HasIndex(e => e.RoleId)
            .HasDatabaseName("IX_MessageThreadMemberRole_RoleId");

        entity.HasOne(d => d.MessageThreadMember).WithMany()
            .HasForeignKey(d => d.MessageThreadMemberId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagethreadmemberrole_messagethreadmember_id_fk");

        entity.HasOne(d => d.Role).WithMany()
            .HasForeignKey(d => d.RoleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("messagethreadmemberrole_identityrole_id_fk");
    }
}
