using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class PaymentGatewayConfiguration : IEntityTypeConfiguration<PaymentGateway>
{
    public void Configure(EntityTypeBuilder<PaymentGateway> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_gateways_pk");

        entity.ToTable("Gateway", "Integration.PaymentGateway");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.ConvenienceFee).HasPrecision(10, 2);
        entity.Property(e => e.Description).HasColumnType("character varying");
        entity.Property(e => e.Discount)
            .HasPrecision(10, 2)
            .HasDefaultValueSql("0");
        entity.Property(e => e.GatewayCategoryId).HasColumnName("GatewayCategoryID");

        entity.Property(e => e.Image).HasColumnType("character varying");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.IsEnabled).HasDefaultValueSql("true");
        entity.Property(e => e.Name).HasColumnType("character varying");
        entity.Property(e => e.ServiceCharge).HasPrecision(3, 2);

        entity.HasOne(d => d.PaymentGatewayCategory).WithMany(p => p.Gateways)
            .HasForeignKey(d => d.GatewayCategoryId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tbl_gateways_tbl_gatewaycategories_id_fk");

        entity.HasOne(d => d.ProviderEndpoint).WithMany(p => p.Gateways)
            .HasForeignKey(d => d.ProviderEndpointId)
            .HasConstraintName("tbl_gateways_tbl_providerendpoints_id_fk");
    }
}
