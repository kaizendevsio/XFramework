using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class SessionTypeConfiguration : IEntityTypeConfiguration<SessionType>
{
    public void Configure(EntityTypeBuilder<SessionType> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_SessionType");

        entity.ToTable("SessionType", "Identity");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.Name).HasColumnType("character varying");
    }
}
