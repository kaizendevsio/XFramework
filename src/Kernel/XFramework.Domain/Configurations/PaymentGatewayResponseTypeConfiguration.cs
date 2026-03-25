using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class PaymentGatewayResponseTypeConfiguration : IEntityTypeConfiguration<PaymentGatewayResponseType>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayResponseType> entity)
    {
        entity.HasKey(e => e.Id).HasName("gatewayresponsetype_pk");

        entity.ToTable("GatewayResponseType", "Integration.PaymentGateway");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Code).HasColumnType("character varying");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");

        entity.HasOne(d => d.PaymentGatewayType).WithMany(p => p.GatewayResponseTypes)
            .HasForeignKey(d => d.GatewayTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("gatewayresponsetype_gatewayTypes_id_fk");
    }
}
