using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.Update;

public sealed class UpdateProductVariationValidator : AbstractValidator<UpdateProductVariationRequest>
{
    public UpdateProductVariationValidator()
    {
        RuleFor(x => x.ProductVariationId).NotEmpty().WithMessage("Variant is required.");
        RuleFor(x => x.ProductVariationTypeId).NotEmpty().WithMessage("Variation type is required.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Variant name is required.")
            .MaximumLength(200).WithMessage("Variant name cannot exceed 200 characters.");
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Variant price cannot be negative.");
    }
}
