using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bolt.Domain.Shared.Configurations;

public sealed class BoltServiceManifestRecordConfiguration : IEntityTypeConfiguration<BoltServiceManifestRecord>
{
    public void Configure(EntityTypeBuilder<BoltServiceManifestRecord> entity)
    {
        entity.HasKey(e => e.Id).HasName("boltservicemanifest_pk");

        entity.ToTable("ServiceManifest", "Bolt");

        entity.HasIndex(e => e.ClientId)
            .IsUnique()
            .HasDatabaseName("IX_ServiceManifest_ClientId");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");

        entity.Property(e => e.ClientId)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnType("character varying");

        entity.Property(e => e.ClientName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("character varying");

        entity.Property(e => e.ServiceName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("character varying");

        entity.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("character varying");

        entity.Property(e => e.Version)
            .HasMaxLength(64)
            .HasColumnType("character varying");

        entity.Property(e => e.ManifestHash)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("character varying");

        entity.Property(e => e.ManifestJson)
            .IsRequired()
            .HasColumnType("jsonb");

        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
    }
}
