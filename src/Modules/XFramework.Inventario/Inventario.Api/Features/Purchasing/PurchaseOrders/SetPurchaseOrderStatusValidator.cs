using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Features.Purchasing.PurchaseOrders;

public sealed class SetPurchaseOrderStatusValidator : AbstractValidator<SetPurchaseOrderStatusRequest>
{
    public SetPurchaseOrderStatusValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty();

        RuleFor(x => x.Status)
            .Must(value => Enum.IsDefined(typeof(PurchaseOrderStatus), value))
            .WithMessage("Purchase order status is invalid.");

        RuleFor(x => x.Status)
            .Must(status => status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Open or PurchaseOrderStatus.Cancelled)
            .WithMessage("Purchase orders can only be set to draft, open, or cancelled directly.");
    }
}
