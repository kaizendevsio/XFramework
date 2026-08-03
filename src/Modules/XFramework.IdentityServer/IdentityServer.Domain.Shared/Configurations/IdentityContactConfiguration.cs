using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityContactConfiguration : IEntityTypeConfiguration<IdentityContact>
{
    public void Configure(EntityTypeBuilder<IdentityContact> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityContacts");

        entity.ToTable("IdentityContact", "Identity");

        entity.HasIndex(e => e.TypeId, "IX_tbl_IdentityContacts_TypeID");

        entity.HasIndex(e => e.CredentialId, "tbl_identitycontacts_CredentialID_index");

        entity.HasIndex(e => new { e.TenantId, e.Value, e.TypeId })
            .HasDatabaseName("UX_IdentityContact_ActiveAuthenticationContact")
            .IsUnique()
            .HasFilter(
                $"\"IsDeleted\" = false AND \"IsEnabled\" = true AND \"TypeId\" IN ('{IdentityConstants.ContactType.Email}'::uuid, '{IdentityConstants.ContactType.Phone}'::uuid)");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();

        entity.Property(e => e.CredentialId).HasColumnName("CredentialID");
        entity.Property(e => e.Value).HasColumnType("character varying");

        entity.HasOne(d => d.Type).WithMany(p => p.IdentityContacts)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("IdentityContact_TypeID");

        entity.HasOne(d => d.Group).WithMany(p => p.IdentityContacts)
            .HasForeignKey(d => d.GroupId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("identitycontact_identitycontactgroup__fk");

        entity.HasOne(d => d.Credential).WithMany(p => p.IdentityContacts)
            .HasForeignKey(d => d.CredentialId)
            .HasConstraintName("tbl_identitycontacts___fk");
    }
}
