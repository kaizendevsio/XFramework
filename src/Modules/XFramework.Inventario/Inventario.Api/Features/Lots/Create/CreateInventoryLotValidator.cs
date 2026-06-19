using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

namespace Inventario.Api.Features.Lots.Create;

public sealed class CreateInventoryLotValidator : AbstractValidator<CreateInventoryLotRequest>
{
    public CreateInventoryLotValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product is required.");

        RuleFor(x => x.LotNumber)
            .NotEmpty().WithMessage("Lot number is required.")
            .MaximumLength(100).WithMessage("Lot number cannot exceed 100 characters.");

        RuleFor(x => x.SupplierReference)
            .MaximumLength(200).WithMessage("Supplier reference cannot exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.SupplierReference));

        RuleFor(x => x.SourceReferenceType)
            .MaximumLength(100).WithMessage("Source reference type cannot exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.SourceReferenceType));

        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0).WithMessage("Unit cost cannot be negative.")
            .When(x => x.UnitCost.HasValue);

        RuleFor(x => x)
            .Must(x => x.ExpiresAt is null || x.ManufacturedAt is null || x.ExpiresAt > x.ManufacturedAt)
            .WithMessage("Expiration date must be after manufacture date.");

        RuleFor(x => x)
            .Must(x => x.ReceivedAt is null || x.ManufacturedAt is null || x.ReceivedAt >= x.ManufacturedAt)
            .WithMessage("Received date must be on or after manufacture date.");
    }
}
