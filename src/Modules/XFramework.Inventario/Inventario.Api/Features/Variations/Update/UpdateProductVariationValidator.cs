using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.Update;

public sealed class UpdateProductVariationValidator : AbstractValidator<UpdateProductVariationRequest>
{
    public UpdateProductVariationValidator()
    {
        RuleFor(x => x.ProductVariationId).NotEmpty().WithMessage("Variant is required.");
        RuleFor(x => x.ProductVariationTypeId).NotEmpty().When(x => x.CombinationOptions is null)
            .WithMessage("Variation type is required.");
        RuleFor(x => x.ProductVariationTypeId).Empty().When(x => x.CombinationOptions is not null)
            .WithMessage("A combination uses option types, not a single variation type.");
        RuleFor(x => x.CombinationOptions).Custom((options, context) =>
        {
            if (options is not null && ProductVariationCombination.Validate(options) is { } error)
                context.AddFailure(error);
        });
        RuleFor(x => x.ExpectedConcurrencyStamp).NotNull().NotEqual(Guid.Empty)
            .When(x => x.CombinationOptions is not null);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Variant name is required.")
            .MaximumLength(200).WithMessage("Variant name cannot exceed 200 characters.");
        RuleFor(x => x.Price)
            .InclusiveBetween(0, ProductVariationCombination.MaxPrice)
            .PrecisionScale(18, 2, true);
    }
}
