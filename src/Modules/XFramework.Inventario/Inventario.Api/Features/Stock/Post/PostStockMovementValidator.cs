using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

namespace Inventario.Api.Features.Stock.Post;

public sealed class PostStockMovementValidator : AbstractValidator<PostStockMovementRequest>
{
    public PostStockMovementValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product is required.");
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Warehouse is required.");
        RuleFor(x => x.LocationId).NotEmpty().WithMessage("Location is required.");
        RuleFor(x => x.Quantity).NotEqual(0).WithMessage("Quantity must not be zero.");
        RuleFor(x => x.UnitOfMeasure).MaximumLength(25).When(x => !string.IsNullOrWhiteSpace(x.UnitOfMeasure));
        RuleFor(x => x.ReferenceType).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.ReferenceType));
        RuleFor(x => x.Reason).MaximumLength(1000).When(x => !string.IsNullOrWhiteSpace(x.Reason));
        RuleFor(x => x.IdempotencyKey).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));
    }
}
