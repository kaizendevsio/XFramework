using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class PaymentGatewayResponseStatusTypeConfiguration : IEntityTypeConfiguration<PaymentGatewayResponseStatusType>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayResponseStatusType> entity)
    {
        entity.HasKey(e => e.Id).HasName("gatewayresponsestatustype_pk");

        entity.ToTable("GatewayResponseStatusType", "Integration.PaymentGateway");

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
    }
}
