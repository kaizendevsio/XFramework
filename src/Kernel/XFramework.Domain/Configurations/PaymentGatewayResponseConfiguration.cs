using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class PaymentGatewayResponseConfiguration : IEntityTypeConfiguration<PaymentGatewayResponse>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayResponse> entity)
    {
        entity.HasKey(e => e.Id).HasName("gatewayresponse_pk");

        entity.ToTable("GatewayResponse", "Integration.PaymentGateway");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Code).HasColumnType("character varying");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Description).HasColumnType("character varying");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.Message).HasColumnType("character varying");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasOne(d => d.PaymentGatewayResponseType).WithMany(p => p.GatewayResponses)
            .HasForeignKey(d => d.GatewayResponseTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("gatewayresponse_gatewayresponsetype_id_fk");

        entity.HasOne(d => d.ResponseStatusType).WithMany(p => p.GatewayResponses)
            .HasForeignKey(d => d.ResponseStatusTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("gatewayresponse_gatewayresponsestatustype_id_fk");
    }
}
