using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.Create;

public sealed class CreateProductVariationValidator : AbstractValidator<CreateProductVariationRequest>
{
    public CreateProductVariationValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product is required.");
        RuleFor(x => x.ProductVariationTypeId).NotEmpty().When(x => x.CombinationOptions is null)
            .WithMessage("Variation type is required.");
        RuleFor(x => x.ProductVariationTypeId).Empty().When(x => x.CombinationOptions is not null)
            .WithMessage("A combination uses option types, not a single variation type.");
        RuleFor(x => x.CombinationOptions).Custom((options, context) =>
        {
            if (options is not null && ProductVariationCombination.Validate(options) is { } error)
                context.AddFailure(error);
        });
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Variant name is required.")
            .MaximumLength(200).WithMessage("Variant name cannot exceed 200 characters.");
        RuleFor(x => x.Price)
            .InclusiveBetween(0, ProductVariationCombination.MaxPrice)
            .PrecisionScale(18, 2, true);
    }
}
