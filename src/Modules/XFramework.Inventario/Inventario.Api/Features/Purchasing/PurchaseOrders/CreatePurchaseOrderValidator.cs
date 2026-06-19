using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Features.Purchasing.PurchaseOrders;

public sealed class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderValidator()
    {
        RuleFor(x => x.OrderNumber).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Status)
            .Must(value => Enum.IsDefined(typeof(PurchaseOrderStatus), value))
            .WithMessage("Purchase order status is invalid.");
        RuleFor(x => x.Status)
            .Must(x => x is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Open)
            .WithMessage("New purchase orders can only start as draft or open.");
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.ProductId).NotEmpty();
            line.RuleFor(x => x.OrderedQuantity).GreaterThan(0);
            line.RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
            line.RuleFor(x => x.UnitOfMeasure).MaximumLength(25);
            line.RuleFor(x => x.Notes).MaximumLength(1000);
        });
    }
}
