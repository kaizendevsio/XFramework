using FluentValidation;
using XFramework.Inventario.Api.Services;

namespace Inventario.Api.Features.Products.Create;

/// <summary>
/// Validator for CreateProductRequest
/// </summary>
public class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity cannot be negative");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Category is required");

        RuleFor(x => x.SKU)
            .MaximumLength(50).WithMessage("SKU cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.SKU));

        RuleFor(x => x.Brand)
            .MaximumLength(100).WithMessage("Brand cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Brand));

        RuleFor(x => x.Weight)
            .GreaterThan(0).WithMessage("Weight must be greater than 0")
            .When(x => x.Weight.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Image)
            .MaximumLength(2048).WithMessage("Image cannot exceed 2048 characters")
            .When(x => !string.IsNullOrEmpty(x.Image));
    }
}
