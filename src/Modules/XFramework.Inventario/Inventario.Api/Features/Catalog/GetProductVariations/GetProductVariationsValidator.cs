using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

namespace Inventario.Api.Features.Catalog.GetProductVariations;

public sealed class GetProductVariationsValidator : AbstractValidator<GetProductVariationsRequest>
{
    public GetProductVariationsValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product is required.");
    }
}
