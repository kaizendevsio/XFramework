using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;

namespace Inventario.Api.Features.Brands.Search;

public sealed class SearchProductBrandsValidator : AbstractValidator<SearchProductBrandsRequest>
{
    public SearchProductBrandsValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Search).Must(value => (ProductBrand.Normalize(value)?.Length ?? 0) <= ProductBrand.MaxLength);
    }
}

