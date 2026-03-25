using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> entity)
    {
        entity.HasKey(e => e.Id).HasName("subscription_pk");

        entity.ToTable("Subscription", "Affiliate");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.CredentialId).HasColumnName("CredentialID");
        entity.Property(e => e.TypeId).HasColumnName("TypeID");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Value).HasColumnType("character varying");

        // NOTE: Cross-module reference — IdentityCredential is from IdentityServer.Domain.Shared.Contracts
        entity.HasOne(d => d.Credential).WithMany(p => p.Subscriptions)
            .HasForeignKey(d => d.CredentialId)
            .HasConstraintName("subscription_identitycredential_id_fk");

        entity.HasOne(d => d.Type).WithMany(p => p.Subscriptions)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("subscription_subscriptionentity_id_fk");
    }
}
