using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Features.Stock.Post;

public sealed class PostStockMovementValidator : AbstractValidator<PostStockMovementRequest>
{
    public PostStockMovementValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product is required.");
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Warehouse is required.");
        RuleFor(x => x.LocationId).NotEmpty().WithMessage("Location is required.");
        RuleFor(x => x.MovementType)
            .Must(value => Enum.IsDefined(typeof(InventoryMovementType), value))
            .WithMessage("Movement type is invalid.");
        RuleFor(x => x.Quantity).NotEqual(0).WithMessage("Quantity must not be zero.");
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .When(x => x.MovementType != InventoryMovementType.Adjustment)
            .WithMessage("Quantity must be positive for this movement type.");
        RuleFor(x => x.DestinationWarehouseId)
            .NotEmpty()
            .When(x => x.MovementType == InventoryMovementType.Transfer)
            .WithMessage("Transfer destination warehouse is required.");
        RuleFor(x => x.DestinationLocationId)
            .NotEmpty()
            .When(x => x.MovementType == InventoryMovementType.Transfer)
            .WithMessage("Transfer destination location is required.");
        RuleFor(x => x.UnitOfMeasure).MaximumLength(25).When(x => !string.IsNullOrWhiteSpace(x.UnitOfMeasure));
        RuleFor(x => x.ReferenceType).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.ReferenceType));
        RuleFor(x => x.Reason).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Reason));
        RuleFor(x => x.IdempotencyKey).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));
    }
}
