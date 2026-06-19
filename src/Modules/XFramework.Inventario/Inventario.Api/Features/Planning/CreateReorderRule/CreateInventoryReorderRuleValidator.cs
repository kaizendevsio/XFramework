using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;

namespace Inventario.Api.Features.Planning.CreateReorderRule;

public sealed class CreateInventoryReorderRuleValidator : AbstractValidator<CreateInventoryReorderRuleRequest>
{
    public CreateInventoryReorderRuleValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.MinimumQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderPoint).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderQuantity).GreaterThan(0);
        RuleFor(x => x.MaximumQuantity)
            .GreaterThanOrEqualTo(x => x.MinimumQuantity)
            .When(x => x.MaximumQuantity is not null);
        RuleFor(x => x.PreferredSupplier).MaximumLength(200);
    }
}
