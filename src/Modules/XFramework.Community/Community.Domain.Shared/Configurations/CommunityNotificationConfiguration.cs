using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Configurations;

public class CommunityNotificationConfiguration : IEntityTypeConfiguration<CommunityNotification>
{
    public void Configure(EntityTypeBuilder<CommunityNotification> entity)
    {
        entity.HasKey(e => e.Id).HasName("communitynotification_pk");

        entity.ToTable("CommunityNotification", "Community");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Message).HasColumnType("character varying");

        entity.HasOne(d => d.RecipientIdentity)
            .WithMany()
            .HasForeignKey(d => d.RecipientIdentityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("communitynotification_recipientidentity_id_fk");

        entity.HasOne(d => d.ActorIdentity)
            .WithMany()
            .HasForeignKey(d => d.ActorIdentityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("communitynotification_actoridentity_id_fk");
    }
}
