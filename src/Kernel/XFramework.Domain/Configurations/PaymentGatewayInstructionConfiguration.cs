using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Configurations;

public class PaymentGatewayInstructionConfiguration : IEntityTypeConfiguration<PaymentGatewayInstruction>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayInstruction> entity)
    {
        entity.HasKey(e => e.Id).HasName("GatewayInstructions_pk");

        entity.ToTable("GatewayInstructions", "Integration.PaymentGateway");

        entity.Property(e => e.ExampleText).HasColumnType("character varying");
        entity.Property(e => e.InstructionText).HasColumnType("character varying");
        entity.Property(e => e.Note).HasColumnType("character varying");

        entity.HasOne(d => d.Gateway).WithMany(p => p.GatewayInstructions)
            .HasForeignKey(d => d.GatewayId)
            .HasConstraintName("GatewayInstructions_Gateways_ID_fk");
    }
}
