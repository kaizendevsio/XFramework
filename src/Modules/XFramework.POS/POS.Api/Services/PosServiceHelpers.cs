using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace POS.Api.Services;

internal static class PosServiceHelpers
{
    public const string SaleLineReferenceType = "POS.SaleLine";
    public const string ReturnLineReferenceType = "POS.ReturnLine";

    public static (int Page, int PageSize) NormalizePage(int page, int pageSize, int defaultPageSize = 50)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? defaultPageSize : Math.Min(pageSize, 100);
        return (normalizedPage, normalizedPageSize);
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string NewSaleNumber(DateTime now, Guid saleId) =>
        $"POS-{now:yyyyMMddHHmmss}-{saleId:N}"[..30];

    public static string NewReturnNumber(DateTime now, Guid returnId) =>
        $"RET-{now:yyyyMMddHHmmss}-{returnId:N}"[..30];

    public static string NewCartNumber(DateTime now, Guid cartId) =>
        $"CART-{now:yyyyMMddHHmmss}-{cartId:N}"[..32];

    public static string SalePaymentReference(PosSale sale, PosPayment payment) =>
        $"POS-SALE-{sale.Id:N}-{payment.Id:N}"[..80];

    public static string SaleLineReservationReference(PosSaleLine line) =>
        $"POS-LINE-{line.Id:N}"[..80];

    public static string ReturnRefundReference(PosReturn posReturn) =>
        $"POS-RETURN-{posReturn.Id:N}"[..80];

    public static string BuildSaleRequestHash(CheckoutPosSaleRequest request) =>
        Hash(new
        {
            request.RegisterId,
            request.CashierCredentialId,
            request.CustomerCredentialId,
            request.WarehouseId,
            request.LocationId,
            request.CurrencyId,
            request.WalletTypeId,
            request.DiscountAmount,
            request.TaxAmount,
            Payment = new
            {
                request.Payment.Method,
                request.Payment.Amount,
                request.Payment.CustomerCredentialId
            },
            Lines = request.Lines.Select((line, index) => new
            {
                index,
                line.ProductId,
                line.ProductVariationId,
                line.Quantity,
                line.ExpectedUnitPrice,
                line.DiscountAmount,
                line.TaxAmount,
                line.WarehouseId,
                line.LocationId,
                line.LotId,
                line.UnitOfMeasure
            }).ToArray()
        });

    public static string BuildReturnRequestHash(CreatePosReturnRequest request) =>
        Hash(new
        {
            request.SaleId,
            request.CashierCredentialId,
            request.RefundMethod,
            request.Reason,
            Lines = request.Lines.Select((line, index) => new
            {
                index,
                line.SaleLineId,
                line.Quantity,
                line.TaxAmount
            }).ToArray()
        });

    private static string Hash<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public static PosRegisterResponse ToRegisterResponse(PosRegister register) => new()
    {
        Id = register.Id,
        TenantId = register.TenantId,
        Name = register.Name,
        Code = register.Code,
        MerchantCredentialId = register.MerchantCredentialId,
        CashDrawerWalletId = register.CashDrawerWalletId,
        WalletTypeId = register.WalletTypeId,
        CurrencyId = register.CurrencyId,
        DefaultWarehouseId = register.DefaultWarehouseId,
        DefaultLocationId = register.DefaultLocationId,
        Description = register.Description,
        IsEnabled = register.IsEnabled
    };

    public static PosCartResponse ToCartResponse(
        PosCart cart,
        IReadOnlyCollection<string>? warnings = null) => new()
    {
        Id = cart.Id,
        TenantId = cart.TenantId,
        CartNumber = cart.CartNumber,
        RegisterId = cart.RegisterId,
        CashierCredentialId = cart.CashierCredentialId,
        CustomerCredentialId = cart.CustomerCredentialId,
        CustomerLabel = cart.CustomerLabel,
        Notes = cart.Notes,
        WarehouseId = cart.WarehouseId,
        LocationId = cart.LocationId,
        Status = cart.Status,
        SubtotalAmount = cart.SubtotalAmount,
        DiscountAmount = cart.DiscountAmount,
        TaxAmount = cart.TaxAmount,
        TotalAmount = cart.TotalAmount,
        CurrencyId = cart.CurrencyId,
        WalletTypeId = cart.WalletTypeId,
        IdempotencyKey = cart.IdempotencyKey,
        SuspendedAt = cart.SuspendedAt,
        ResumedAt = cart.ResumedAt,
        ExpiresAt = cart.ExpiresAt,
        ConvertedSaleId = cart.ConvertedSaleId,
        CancelledAt = cart.CancelledAt,
        CancelReason = cart.CancelReason,
        CreatedAt = cart.CreatedAt,
        ModifiedAt = cart.ModifiedAt,
        ConcurrencyStamp = cart.ConcurrencyStamp,
        Warnings = warnings?.ToList() ?? [],
        Lines = cart.Lines
            .OrderBy(line => line.LineNumber)
            .Select(ToCartLineResponse)
            .ToList()
    };

    public static PosCartSummaryResponse ToCartSummaryResponse(PosCart cart) => new()
    {
        Id = cart.Id,
        TenantId = cart.TenantId,
        CartNumber = cart.CartNumber,
        RegisterId = cart.RegisterId,
        CashierCredentialId = cart.CashierCredentialId,
        CustomerCredentialId = cart.CustomerCredentialId,
        CustomerLabel = cart.CustomerLabel,
        Status = cart.Status,
        TotalAmount = cart.TotalAmount,
        CurrencyId = cart.CurrencyId,
        CreatedAt = cart.CreatedAt,
        SuspendedAt = cart.SuspendedAt,
        ExpiresAt = cart.ExpiresAt,
        ConvertedSaleId = cart.ConvertedSaleId,
        ConcurrencyStamp = cart.ConcurrencyStamp
    };

    public static PosSaleReceiptResponse ToSaleReceiptResponse(PosSale sale) => new()
    {
        Id = sale.Id,
        TenantId = sale.TenantId,
        SaleNumber = sale.SaleNumber,
        RegisterId = sale.RegisterId,
        CashierCredentialId = sale.CashierCredentialId,
        CustomerCredentialId = sale.CustomerCredentialId,
        Status = sale.Status,
        SubtotalAmount = sale.SubtotalAmount,
        DiscountAmount = sale.DiscountAmount,
        TaxAmount = sale.TaxAmount,
        TotalAmount = sale.TotalAmount,
        CurrencyId = sale.CurrencyId,
        WalletTypeId = sale.WalletTypeId,
        PaymentMethod = sale.PaymentMethod,
        IdempotencyKey = sale.IdempotencyKey,
        CompletedAt = sale.CompletedAt,
        FailureReason = sale.FailureReason,
        RecoveryState = sale.RecoveryState,
        Lines = sale.Lines
            .OrderBy(line => line.LineNumber)
            .Select(ToSaleLineResponse)
            .ToList(),
        Payments = sale.Payments
            .OrderBy(payment => payment.CreatedAt)
            .Select(ToPaymentResponse)
            .ToList()
    };

    public static PosSaleSummaryResponse ToSaleSummaryResponse(PosSale sale) => new()
    {
        Id = sale.Id,
        TenantId = sale.TenantId,
        SaleNumber = sale.SaleNumber,
        RegisterId = sale.RegisterId,
        Status = sale.Status,
        TotalAmount = sale.TotalAmount,
        CurrencyId = sale.CurrencyId,
        CreatedAt = sale.CreatedAt,
        CompletedAt = sale.CompletedAt,
        FailureReason = sale.FailureReason
    };

    public static PosReturnResponse ToReturnResponse(PosReturn posReturn) => new()
    {
        Id = posReturn.Id,
        TenantId = posReturn.TenantId,
        ReturnNumber = posReturn.ReturnNumber,
        SaleId = posReturn.SaleId,
        SaleNumber = posReturn.Sale.SaleNumber,
        RegisterId = posReturn.RegisterId,
        CashierCredentialId = posReturn.CashierCredentialId,
        CustomerCredentialId = posReturn.CustomerCredentialId,
        Status = posReturn.Status,
        RefundMethod = posReturn.RefundMethod,
        TotalRefundAmount = posReturn.TotalRefundAmount,
        CurrencyId = posReturn.CurrencyId,
        WalletTypeId = posReturn.WalletTypeId,
        Reason = posReturn.Reason,
        IdempotencyKey = posReturn.IdempotencyKey,
        RefundReferenceNumber = posReturn.RefundReferenceNumber,
        CompletedAt = posReturn.CompletedAt,
        FailureReason = posReturn.FailureReason,
        Lines = posReturn.Lines
            .OrderBy(line => line.CreatedAt)
            .Select(ToReturnLineResponse)
            .ToList()
    };

    public static PosReturnSummaryResponse ToReturnSummaryResponse(PosReturn posReturn) => new()
    {
        Id = posReturn.Id,
        TenantId = posReturn.TenantId,
        ReturnNumber = posReturn.ReturnNumber,
        SaleId = posReturn.SaleId,
        RegisterId = posReturn.RegisterId,
        Status = posReturn.Status,
        TotalRefundAmount = posReturn.TotalRefundAmount,
        CurrencyId = posReturn.CurrencyId,
        CreatedAt = posReturn.CreatedAt,
        CompletedAt = posReturn.CompletedAt,
        FailureReason = posReturn.FailureReason
    };

    private static PosCartLineResponse ToCartLineResponse(PosCartLine line) => new()
    {
        Id = line.Id,
        LineNumber = line.LineNumber,
        ProductId = line.ProductId,
        ProductVariationId = line.ProductVariationId,
        ProductName = line.ProductName,
        VariantName = line.VariantName,
        SKU = line.SKU,
        Quantity = line.Quantity,
        UnitPrice = line.UnitPrice,
        ExpectedUnitPrice = line.ExpectedUnitPrice,
        DiscountAmount = line.DiscountAmount,
        TaxAmount = line.TaxAmount,
        LineTotal = line.LineTotal,
        WarehouseId = line.WarehouseId,
        LocationId = line.LocationId,
        LotId = line.LotId
    };

    private static PosSaleLineResponse ToSaleLineResponse(PosSaleLine line) => new()
    {
        Id = line.Id,
        LineNumber = line.LineNumber,
        ProductId = line.ProductId,
        ProductVariationId = line.ProductVariationId,
        ProductName = line.ProductName,
        VariantName = line.VariantName,
        SKU = line.SKU,
        Quantity = line.Quantity,
        UnitPrice = line.UnitPrice,
        DiscountAmount = line.DiscountAmount,
        TaxAmount = line.TaxAmount,
        LineTotal = line.LineTotal,
        WarehouseId = line.WarehouseId,
        LocationId = line.LocationId,
        LotId = line.LotId,
        ReservationId = line.ReservationId,
        FulfilledAt = line.FulfilledAt,
        FailureReason = line.FailureReason
    };

    private static PosPaymentResponse ToPaymentResponse(PosPayment payment) => new()
    {
        Id = payment.Id,
        Method = payment.Method,
        Status = payment.Status,
        Amount = payment.Amount,
        ReferenceNumber = payment.ReferenceNumber,
        IdempotencyKey = payment.IdempotencyKey,
        FailureReason = payment.FailureReason,
        CapturedAt = payment.CapturedAt
    };

    private static PosReturnLineResponse ToReturnLineResponse(PosReturnLine line) => new()
    {
        Id = line.Id,
        SaleLineId = line.SaleLineId,
        ProductId = line.ProductId,
        ProductVariationId = line.ProductVariationId,
        ProductName = line.ProductName,
        VariantName = line.VariantName,
        Quantity = line.Quantity,
        UnitPrice = line.UnitPrice,
        TaxAmount = line.TaxAmount,
        RefundAmount = line.RefundAmount,
        WarehouseId = line.WarehouseId,
        LocationId = line.LocationId,
        LotId = line.LotId,
        InventoryMovementReferenceNumber = line.InventoryMovementReferenceNumber,
        FailureReason = line.FailureReason
    };
}
