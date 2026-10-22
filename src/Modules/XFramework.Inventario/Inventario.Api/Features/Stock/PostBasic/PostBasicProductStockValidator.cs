using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

namespace Inventario.Api.Features.Stock.PostBasic;

public sealed class PostBasicProductStockValidator : AbstractValidator<PostBasicProductStockRequest>
{
    public PostBasicProductStockValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductVariationId).NotEqual(Guid.Empty).When(x => x.ProductVariationId.HasValue);
        RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(18, 4, true);
        RuleFor(x => x.Reason).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
