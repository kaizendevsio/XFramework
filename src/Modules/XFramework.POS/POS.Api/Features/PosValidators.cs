using FluentValidation;
using POS.Domain.Shared.Contracts.Requests;

namespace POS.Api.Features;

public sealed class SearchPosCatalogValidator : AbstractValidator<SearchPosCatalogRequest>
{
    public SearchPosCatalogValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetPosRegisterValidator : AbstractValidator<GetPosRegisterRequest>
{
    public GetPosRegisterValidator() =>
        RuleFor(x => x.Id).NotEmpty();
}

public sealed class CreatePosRegisterValidator : AbstractValidator<CreatePosRegisterRequest>
{
    public CreatePosRegisterValidator() => Configure(this);

    internal static void Configure<TRequest>(AbstractValidator<TRequest> validator)
        where TRequest : CreatePosRegisterRequest
    {
        validator.RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        validator.RuleFor(x => x.Code).MaximumLength(64).When(x => !string.IsNullOrWhiteSpace(x.Code));
        validator.RuleFor(x => x.MerchantCredentialId).NotEmpty();
        validator.RuleFor(x => x.CashDrawerWalletId).NotEmpty();
        validator.RuleFor(x => x.WalletTypeId).NotEmpty();
        validator.RuleFor(x => x.CurrencyId).NotEmpty();
        validator.RuleFor(x => x.DefaultWarehouseId).NotEmpty();
        validator.RuleFor(x => x.DefaultLocationId).NotEmpty();
        validator.RuleFor(x => x.Description).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class UpdatePosRegisterValidator : AbstractValidator<UpdatePosRegisterRequest>
{
    public UpdatePosRegisterValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(64).When(x => !string.IsNullOrWhiteSpace(x.Code));
        RuleFor(x => x.MerchantCredentialId).NotEmpty();
        RuleFor(x => x.CashDrawerWalletId).NotEmpty();
        RuleFor(x => x.WalletTypeId).NotEmpty();
        RuleFor(x => x.CurrencyId).NotEmpty();
        RuleFor(x => x.DefaultWarehouseId).NotEmpty();
        RuleFor(x => x.DefaultLocationId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

public sealed class CreatePosCartValidator : AbstractValidator<CreatePosCartRequest>
{
    public CreatePosCartValidator()
    {
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.CashierCredentialId).NotEmpty();
        RuleFor(x => x.CustomerLabel).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.CustomerLabel));
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Notes));
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).MaximumLength(160).When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new PosCartLineValidator());
    }
}

public sealed class UpdatePosCartValidator : AbstractValidator<UpdatePosCartRequest>
{
    public UpdatePosCartValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CustomerLabel).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.CustomerLabel));
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Notes));
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).SetValidator(new PosCartLineValidator());
    }
}

public sealed class PosCartLineValidator : AbstractValidator<PosCartLineRequest>
{
    public PosCartLineValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ExpectedUnitPrice).GreaterThanOrEqualTo(0).When(x => x.ExpectedUnitPrice.HasValue);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
    }
}

public sealed class GetPosCartValidator : AbstractValidator<GetPosCartRequest>
{
    public GetPosCartValidator() =>
        RuleFor(x => x.Id).NotEmpty();
}

public sealed class SearchPosCartsValidator : AbstractValidator<SearchPosCartsRequest>
{
    public SearchPosCartsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}

public sealed class SuspendPosCartValidator : AbstractValidator<SuspendPosCartRequest>
{
    public SuspendPosCartValidator() =>
        RuleFor(x => x.CartId).NotEmpty();
}

public sealed class ResumePosCartValidator : AbstractValidator<ResumePosCartRequest>
{
    public ResumePosCartValidator() =>
        RuleFor(x => x.CartId).NotEmpty();
}

public sealed class CancelPosCartValidator : AbstractValidator<CancelPosCartRequest>
{
    public CancelPosCartValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}

public sealed class CheckoutPosCartValidator : AbstractValidator<CheckoutPosCartRequest>
{
    public CheckoutPosCartValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Payment).NotNull();
        RuleFor(x => x.Payment.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Payment.Method).IsInEnum();
        RuleFor(x => x.Payment.CustomerCredentialId)
            .NotEmpty()
            .When(x => x.Payment.Method == POS.Domain.Shared.Enums.PosPaymentMethod.WalletTransfer);
    }
}

public sealed class CheckoutPosSaleValidator : AbstractValidator<CheckoutPosSaleRequest>
{
    public CheckoutPosSaleValidator()
    {
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.CashierCredentialId).NotEmpty();
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Payment).NotNull();
        RuleFor(x => x.Payment.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Payment.Method).IsInEnum();
        RuleFor(x => x.Payment.CustomerCredentialId)
            .NotEmpty()
            .When(x => x.Payment.Method == POS.Domain.Shared.Enums.PosPaymentMethod.WalletTransfer);

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.ProductId).NotEmpty();
            line.RuleFor(x => x.Quantity).GreaterThan(0);
            line.RuleFor(x => x.ExpectedUnitPrice).GreaterThanOrEqualTo(0);
            line.RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
            line.RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class GetPosSaleValidator : AbstractValidator<GetPosSaleRequest>
{
    public GetPosSaleValidator() =>
        RuleFor(x => x.Id).NotEmpty();
}

public sealed class SearchPosSalesValidator : AbstractValidator<SearchPosSalesRequest>
{
    public SearchPosSalesValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}

public sealed class CancelPosSaleValidator : AbstractValidator<CancelPosSaleRequest>
{
    public CancelPosSaleValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}

public sealed class RetryPosSaleFulfillmentValidator : AbstractValidator<RetryPosSaleFulfillmentRequest>
{
    public RetryPosSaleFulfillmentValidator() =>
        RuleFor(x => x.SaleId).NotEmpty();
}

public sealed class CreatePosReturnValidator : AbstractValidator<CreatePosReturnRequest>
{
    public CreatePosReturnValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.CashierCredentialId).NotEmpty();
        RuleFor(x => x.RefundMethod).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(500).When(x => !string.IsNullOrWhiteSpace(x.Reason));
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.SaleLineId).NotEmpty();
            line.RuleFor(x => x.Quantity).GreaterThan(0);
            line.RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class GetPosReturnValidator : AbstractValidator<GetPosReturnRequest>
{
    public GetPosReturnValidator() =>
        RuleFor(x => x.Id).NotEmpty();
}

public sealed class SearchPosReturnsValidator : AbstractValidator<SearchPosReturnsRequest>
{
    public SearchPosReturnsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}

public sealed class RetryPosReturnValidator : AbstractValidator<RetryPosReturnRequest>
{
    public RetryPosReturnValidator() =>
        RuleFor(x => x.ReturnId).NotEmpty();
}
