using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class PaymentGatewayEndpointConfiguration : IEntityTypeConfiguration<PaymentGatewayEndpoint>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayEndpoint> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_gatewayendpoints_pk");

        entity.ToTable("GatewayEndpoint", "Integration.PaymentGateway");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.BaseUrlEndpoint).HasMaxLength(100);
        entity.Property(e => e.GatewayId).HasColumnName("GatewayID");

        entity.Property(e => e.Name).HasMaxLength(100);
        entity.Property(e => e.UrlEndpoint).HasMaxLength(100);

        entity.HasOne(d => d.Gateway).WithMany(p => p.GatewayEndpoints)
            .HasForeignKey(d => d.GatewayId)
            .HasConstraintName("tbl_gatewayendpoints_tbl_gatewayType_id_fk");
    }
}
