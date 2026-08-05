using FluentValidation;
using POS.Api.Features;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Enums;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
[Category(TestCategories.POS)]
public sealed class PosValidatorTests
{
    [Test]
    public void CheckoutPosSaleValidator_WalletTransferWithoutCustomerCredential_ReturnsValidationError()
    {
        var request = ValidCheckout();
        request.Payment.Method = PosPaymentMethod.WalletTransfer;
        request.Payment.CustomerCredentialId = null;

        var result = new CheckoutPosSaleValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Payment.CustomerCredentialId");
    }

    [Test]
    public void CheckoutPosSaleValidator_NegativeDiscountTaxOrLineAmount_ReturnsValidationErrors()
    {
        var request = ValidCheckout();
        request.DiscountAmount = -1;
        request.TaxAmount = -1;
        request.Lines[0].DiscountAmount = -1;
        request.Lines[0].TaxAmount = -1;

        var result = new CheckoutPosSaleValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(
            [
                nameof(CheckoutPosSaleRequest.DiscountAmount),
                nameof(CheckoutPosSaleRequest.TaxAmount),
                "Lines[0].DiscountAmount",
                "Lines[0].TaxAmount"
            ]);
    }

    [Test]
    public void CheckoutPosSaleValidator_EmptyLinesOrMissingRegister_ReturnsValidationErrors()
    {
        var request = ValidCheckout();
        request.RegisterId = Guid.Empty;
        request.Lines.Clear();

        var result = new CheckoutPosSaleValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(
            [
                nameof(CheckoutPosSaleRequest.RegisterId),
                nameof(CheckoutPosSaleRequest.Lines)
            ]);
    }

    [Test]
    public void CreatePosReturnValidator_InvalidLine_ReturnsValidationErrors()
    {
        var request = new CreatePosReturnRequest
        {
            SaleId = Guid.NewGuid(),
            CashierCredentialId = Guid.NewGuid(),
            RefundMethod = PosPaymentMethod.CashDrawer,
            IdempotencyKey = "return-key",
            Lines =
            [
                new CreatePosReturnLineRequest
                {
                    SaleLineId = Guid.Empty,
                    Quantity = 0,
                    TaxAmount = -1
                }
            ]
        };

        var result = new CreatePosReturnValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(
            [
                "Lines[0].SaleLineId",
                "Lines[0].Quantity",
                "Lines[0].TaxAmount"
            ]);
    }

    [Test]
    public void CreatePosCartValidator_InvalidLine_ReturnsValidationErrors()
    {
        var request = new CreatePosCartRequest
        {
            RegisterId = Guid.NewGuid(),
            CashierCredentialId = Guid.NewGuid(),
            CustomerLabel = new string('x', 201),
            Lines =
            [
                new PosCartLineRequest
                {
                    ProductId = Guid.Empty,
                    Quantity = 0,
                    ExpectedUnitPrice = -1,
                    DiscountAmount = -1,
                    TaxAmount = -1
                }
            ]
        };

        var result = new CreatePosCartValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.PropertyName).Should().Contain(
            [
                nameof(CreatePosCartRequest.CustomerLabel),
                "Lines[0].ProductId",
                "Lines[0].Quantity",
                "Lines[0].ExpectedUnitPrice",
                "Lines[0].DiscountAmount",
                "Lines[0].TaxAmount"
            ]);
    }

    [Test]
    public void CheckoutPosCartValidator_WalletTransferWithoutCustomerCredential_ReturnsValidationError()
    {
        var request = new CheckoutPosCartRequest
        {
            CartId = Guid.NewGuid(),
            IdempotencyKey = "cart-checkout-key",
            Payment = new CheckoutPosPaymentRequest
            {
                Method = PosPaymentMethod.WalletTransfer,
                Amount = 10
            }
        };

        var result = new CheckoutPosCartValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "Payment.CustomerCredentialId");
    }

    [Test]
    public void SearchValidators_PageBounds_ReturnValidationErrors()
    {
        var catalog = new SearchPosCatalogValidator().Validate(new SearchPosCatalogRequest { Page = 0, PageSize = 101 });
        var carts = new SearchPosCartsValidator().Validate(new SearchPosCartsRequest { Page = 0, PageSize = 101 });
        var sales = new SearchPosSalesValidator().Validate(new SearchPosSalesRequest { Page = 0, PageSize = 101 });
        var returns = new SearchPosReturnsValidator().Validate(new SearchPosReturnsRequest { Page = 0, PageSize = 101 });

        catalog.IsValid.Should().BeFalse();
        carts.IsValid.Should().BeFalse();
        sales.IsValid.Should().BeFalse();
        returns.IsValid.Should().BeFalse();
    }

    [Test]
    public void CheckoutAndReturnValidators_MissingIdempotencyKeys_ReturnValidationErrors()
    {
        var sale = ValidCheckout();
        sale.IdempotencyKey = "";
        var cart = new CheckoutPosCartRequest
        {
            CartId = Guid.NewGuid(),
            Payment = new CheckoutPosPaymentRequest
            {
                Method = PosPaymentMethod.CashDrawer,
                Amount = 10
            }
        };
        var posReturn = new CreatePosReturnRequest
        {
            SaleId = Guid.NewGuid(),
            CashierCredentialId = Guid.NewGuid(),
            RefundMethod = PosPaymentMethod.CashDrawer,
            Lines =
            [
                new CreatePosReturnLineRequest
                {
                    SaleLineId = Guid.NewGuid(),
                    Quantity = 1
                }
            ]
        };

        new CheckoutPosSaleValidator().Validate(sale).Errors
            .Should().Contain(error => error.PropertyName == nameof(CheckoutPosSaleRequest.IdempotencyKey));
        new CheckoutPosCartValidator().Validate(cart).Errors
            .Should().Contain(error => error.PropertyName == nameof(CheckoutPosCartRequest.IdempotencyKey));
        new CreatePosReturnValidator().Validate(posReturn).Errors
            .Should().Contain(error => error.PropertyName == nameof(CreatePosReturnRequest.IdempotencyKey));
    }

    [Test]
    public void RetryPosReturnValidator_MissingReturnId_ReturnsValidationError()
    {
        var result = new RetryPosReturnValidator().Validate(new RetryPosReturnRequest());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(RetryPosReturnRequest.ReturnId));
    }

    [Test]
    public void CreatePosReturnValidator_DuplicateLinesOrClientTax_ReturnsValidationErrors()
    {
        var saleLineId = Guid.NewGuid();
        var request = new CreatePosReturnRequest
        {
            SaleId = Guid.NewGuid(),
            CashierCredentialId = Guid.NewGuid(),
            IdempotencyKey = "return-key",
            Lines =
            [
                new CreatePosReturnLineRequest { SaleLineId = saleLineId, Quantity = 1, TaxAmount = 1 },
                new CreatePosReturnLineRequest { SaleLineId = saleLineId, Quantity = 1 }
            ]
        };

        var result = new CreatePosReturnValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreatePosReturnRequest.Lines));
        result.Errors.Should().Contain(error => error.PropertyName == "Lines[0].TaxAmount");
    }

    [Test]
    public void TransactionValidators_MoreThanMaximumLines_ReturnValidationErrors()
    {
        var sale = ValidCheckout();
        sale.Lines = Enumerable.Range(0, 101)
            .Select(_ => new CheckoutPosSaleLineRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                ExpectedUnitPrice = 1
            })
            .ToList();

        var result = new CheckoutPosSaleValidator().Validate(sale);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CheckoutPosSaleRequest.Lines));
    }

    private static CheckoutPosSaleRequest ValidCheckout() => new()
    {
        RegisterId = Guid.NewGuid(),
        CashierCredentialId = Guid.NewGuid(),
        IdempotencyKey = "sale-checkout-key",
        Lines =
        [
            new CheckoutPosSaleLineRequest
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                ExpectedUnitPrice = 10
            }
        ],
        Payment = new CheckoutPosPaymentRequest
        {
            Method = PosPaymentMethod.CashDrawer,
            Amount = 10
        }
    };
}
