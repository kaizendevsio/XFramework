using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace Inventario.Api.Features.Variations.CreateType;

public sealed class CreateProductVariationTypeValidator : AbstractValidator<CreateProductVariationTypeRequest>
{
    public CreateProductVariationTypeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Variation type name is required.")
            .MaximumLength(100).WithMessage("Variation type name cannot exceed 100 characters.");

        RuleFor(x => x.Code)
            .MaximumLength(50).WithMessage("Variation type code cannot exceed 50 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Code));
    }
}
