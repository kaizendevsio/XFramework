using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.Create;

public sealed class CreateProductVariationValidator : AbstractValidator<CreateProductVariationRequest>
{
    public CreateProductVariationValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product is required.");
        RuleFor(x => x.ProductVariationTypeId).NotEmpty().WithMessage("Variation type is required.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Variant name is required.")
            .MaximumLength(200).WithMessage("Variant name cannot exceed 200 characters.");
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Variant price cannot be negative.");
    }
}
