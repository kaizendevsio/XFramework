using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;

namespace Inventario.Api.Features.Brands.PreviewMerge;

public sealed class PreviewProductBrandMergeValidator : AbstractValidator<PreviewProductBrandMergeRequest>
{
    public PreviewProductBrandMergeValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.SourceBrand).Must(value => ProductBrand.Normalize(value) is { Length: > 0 and <= ProductBrand.MaxLength });
        RuleFor(x => x.TargetBrand).Must(value => ProductBrand.Normalize(value) is { Length: > 0 and <= ProductBrand.MaxLength });
    }
}

