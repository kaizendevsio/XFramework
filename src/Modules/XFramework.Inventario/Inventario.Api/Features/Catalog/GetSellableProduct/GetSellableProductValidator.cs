using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

namespace Inventario.Api.Features.Catalog.GetSellableProduct;

public sealed class GetSellableProductValidator : AbstractValidator<GetSellableProductRequest>
{
    public GetSellableProductValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product is required.");
    }
}
