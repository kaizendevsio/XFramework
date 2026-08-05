using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Shared.Contracts;

namespace POS.Domain.Shared.Configurations;

public sealed class PosRegisterConfiguration : IEntityTypeConfiguration<PosRegister>
{
    public void Configure(EntityTypeBuilder<PosRegister> entity)
    {
        entity.ToTable("PosRegister", "POS");
        entity.ConfigurePosBaseModel("PK_POS_Register");

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Code).HasMaxLength(64);
        entity.Property(e => e.Description).HasMaxLength(500);

        entity.HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"Code\" IS NOT NULL AND \"Code\" <> ''")
            .HasDatabaseName("UX_POS_Register_Tenant_Code_Active");
    }
}

public sealed class PosSaleConfiguration : IEntityTypeConfiguration<PosSale>
{
    public void Configure(EntityTypeBuilder<PosSale> entity)
    {
        entity.ToTable("PosSale", "POS");
        entity.ConfigurePosBaseModel("PK_POS_Sale");

        entity.Property(e => e.SaleNumber).IsRequired().HasMaxLength(80);
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.SubtotalAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
        entity.Property(e => e.PaymentMethod).HasConversion<int>();
        entity.Property(e => e.IdempotencyKey).HasMaxLength(160);
        entity.Property(e => e.FailureReason).HasMaxLength(1000);
        entity.Property(e => e.RecoveryState).HasMaxLength(1000);
        entity.Property(e => e.RequestHash).HasMaxLength(64);

        entity.HasIndex(e => new { e.TenantId, e.SaleNumber })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_POS_Sale_Tenant_Number_Active");
        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''")
            .HasDatabaseName("UX_POS_Sale_Tenant_Idempotency_Active");
        entity.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_POS_Sale_Tenant_Status_Created");

        entity.HasOne(e => e.Register)
            .WithMany(e => e.Sales)
            .HasForeignKey(e => e.RegisterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_POS_Sale_Register");
    }
}

public sealed class PosCartConfiguration : IEntityTypeConfiguration<PosCart>
{
    public void Configure(EntityTypeBuilder<PosCart> entity)
    {
        entity.ToTable("PosCart", "POS");
        entity.ConfigurePosBaseModel("PK_POS_Cart");

        entity.Property(e => e.CartNumber).IsRequired().HasMaxLength(80);
        entity.Property(e => e.CustomerLabel).HasMaxLength(200);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.SubtotalAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
        entity.Property(e => e.IdempotencyKey).HasMaxLength(160);
        entity.Property(e => e.CancelReason).HasMaxLength(500);
        entity.Property(e => e.RequestHash).HasMaxLength(64);

        entity.HasIndex(e => new { e.TenantId, e.CartNumber })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_POS_Cart_Tenant_Number_Active");
        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''")
            .HasDatabaseName("UX_POS_Cart_Tenant_Idempotency_Active");
        entity.HasIndex(e => new { e.TenantId, e.RegisterId, e.Status, e.SuspendedAt })
            .HasDatabaseName("IX_POS_Cart_Tenant_Register_Status_Suspended");
        entity.HasIndex(e => new { e.TenantId, e.CashierCredentialId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_POS_Cart_Tenant_Cashier_Status_Created");
        entity.HasIndex(e => new { e.TenantId, e.ExpiresAt })
            .HasDatabaseName("IX_POS_Cart_Tenant_Expires");

        entity.HasOne(e => e.Register)
            .WithMany(e => e.Carts)
            .HasForeignKey(e => e.RegisterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_POS_Cart_Register");
        entity.HasOne<PosSale>()
            .WithMany()
            .HasForeignKey(e => e.ConvertedSaleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_POS_Cart_ConvertedSale");
    }
}

public sealed class PosCartLineConfiguration : IEntityTypeConfiguration<PosCartLine>
{
    public void Configure(EntityTypeBuilder<PosCartLine> entity)
    {
        entity.ToTable("PosCartLine", "POS");
        entity.ConfigurePosBaseModel("PK_POS_CartLine");

        entity.Property(e => e.ProductName).IsRequired().HasMaxLength(240);
        entity.Property(e => e.VariantName).HasMaxLength(240);
        entity.Property(e => e.SKU).HasMaxLength(128);
        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.ExpectedUnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.LineTotal).HasPrecision(18, 2);

        entity.HasIndex(e => new { e.TenantId, e.CartId, e.LineNumber })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_POS_CartLine_Tenant_Cart_Line_Active");
        entity.HasIndex(e => new { e.TenantId, e.ProductId, e.ProductVariationId })
            .HasDatabaseName("IX_POS_CartLine_Tenant_Product");

        entity.HasOne(e => e.Cart)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.CartId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_POS_CartLine_Cart");
    }
}

public sealed class PosSaleLineConfiguration : IEntityTypeConfiguration<PosSaleLine>
{
    public void Configure(EntityTypeBuilder<PosSaleLine> entity)
    {
        entity.ToTable("PosSaleLine", "POS");
        entity.ConfigurePosBaseModel("PK_POS_SaleLine");

        entity.Property(e => e.ProductName).IsRequired().HasMaxLength(240);
        entity.Property(e => e.VariantName).HasMaxLength(240);
        entity.Property(e => e.SKU).HasMaxLength(128);
        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.ExpectedUnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.LineTotal).HasPrecision(18, 2);
        entity.Property(e => e.FailureReason).HasMaxLength(1000);

        entity.HasIndex(e => new { e.TenantId, e.SaleId, e.LineNumber })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_POS_SaleLine_Tenant_Sale_Line_Active");
        entity.HasIndex(e => new { e.TenantId, e.ReservationId })
            .HasFilter("\"ReservationId\" IS NOT NULL")
            .HasDatabaseName("IX_POS_SaleLine_Tenant_Reservation");

        entity.HasOne(e => e.Sale)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_POS_SaleLine_Sale");
    }
}

public sealed class PosPaymentConfiguration : IEntityTypeConfiguration<PosPayment>
{
    public void Configure(EntityTypeBuilder<PosPayment> entity)
    {
        entity.ToTable("PosPayment", "POS");
        entity.ConfigurePosBaseModel("PK_POS_Payment");

        entity.Property(e => e.Method).HasConversion<int>();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.ReferenceNumber).IsRequired().HasMaxLength(120);
        entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(160);
        entity.Property(e => e.FailureReason).HasMaxLength(1000);
        entity.Property(e => e.RefundedAmount).HasPrecision(18, 2);

        entity.HasIndex(e => new { e.TenantId, e.SaleId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_POS_Payment_Tenant_Sale_Active");
        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_POS_Payment_Tenant_Idempotency_Active");

        entity.HasOne(e => e.Sale)
            .WithMany(e => e.Payments)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_POS_Payment_Sale");
    }
}

public sealed class PosReturnConfiguration : IEntityTypeConfiguration<PosReturn>
{
    public void Configure(EntityTypeBuilder<PosReturn> entity)
    {
        entity.ToTable("PosReturn", "POS");
        entity.ConfigurePosBaseModel("PK_POS_Return");

        entity.Property(e => e.ReturnNumber).IsRequired().HasMaxLength(80);
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.RefundMethod).HasConversion<int>();
        entity.Property(e => e.SubtotalAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.TotalRefundAmount).HasPrecision(18, 2);
        entity.Property(e => e.Reason).HasMaxLength(500);
        entity.Property(e => e.IdempotencyKey).HasMaxLength(160);
        entity.Property(e => e.RefundReferenceNumber).HasMaxLength(120);
        entity.Property(e => e.FailureReason).HasMaxLength(1000);
        entity.Property(e => e.RequestHash).HasMaxLength(64);

        entity.HasIndex(e => new { e.TenantId, e.ReturnNumber })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_POS_Return_Tenant_Number_Active");
        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"IdempotencyKey\" IS NOT NULL AND \"IdempotencyKey\" <> ''")
            .HasDatabaseName("UX_POS_Return_Tenant_Idempotency_Active");
        entity.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_POS_Return_Tenant_Status_Created");

        entity.HasOne(e => e.Sale)
            .WithMany(e => e.Returns)
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_POS_Return_Sale");
        entity.HasOne(e => e.Register)
            .WithMany(e => e.Returns)
            .HasForeignKey(e => e.RegisterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_POS_Return_Register");
    }
}

public sealed class PosReturnLineConfiguration : IEntityTypeConfiguration<PosReturnLine>
{
    public void Configure(EntityTypeBuilder<PosReturnLine> entity)
    {
        entity.ToTable("PosReturnLine", "POS");
        entity.ConfigurePosBaseModel("PK_POS_ReturnLine");

        entity.Property(e => e.ProductName).IsRequired().HasMaxLength(240);
        entity.Property(e => e.VariantName).HasMaxLength(240);
        entity.Property(e => e.Quantity).HasPrecision(18, 4);
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
        entity.Property(e => e.InventoryMovementReferenceNumber).HasMaxLength(120);
        entity.Property(e => e.FailureReason).HasMaxLength(1000);

        entity.HasIndex(e => new { e.TenantId, e.ReturnId })
            .HasDatabaseName("IX_POS_ReturnLine_Tenant_Return");
        entity.HasIndex(e => new { e.TenantId, e.SaleLineId })
            .HasDatabaseName("IX_POS_ReturnLine_Tenant_SaleLine");

        entity.HasOne(e => e.Return)
            .WithMany(e => e.Lines)
            .HasForeignKey(e => e.ReturnId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_POS_ReturnLine_Return");
        entity.HasOne(e => e.SaleLine)
            .WithMany(e => e.ReturnLines)
            .HasForeignKey(e => e.SaleLineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_POS_ReturnLine_SaleLine");
    }
}
