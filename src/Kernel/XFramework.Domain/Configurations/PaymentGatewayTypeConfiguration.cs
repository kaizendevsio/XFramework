using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class PaymentGatewayTypeConfiguration : IEntityTypeConfiguration<PaymentGatewayType>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayType> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_gatewayType_pk");

        entity.ToTable("GatewayType", "Integration.PaymentGateway");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Description).HasColumnType("character varying");

        entity.Property(e => e.IsDeleted)
            .HasDefaultValueSql("false")
            .HasColumnName("isDeleted");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true")
            .HasColumnName("isEnabled");
        entity.Property(e => e.Name).HasColumnType("character varying");
    }
}
