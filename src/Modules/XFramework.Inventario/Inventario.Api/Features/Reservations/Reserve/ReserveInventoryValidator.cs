using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Reserve;

public sealed class ReserveInventoryValidator : AbstractValidator<ReserveInventoryRequest>
{
    public ReserveInventoryValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ReferenceType).MaximumLength(100);
        RuleFor(x => x.UnitOfMeasure).MaximumLength(20);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.ExpiredLotOverrideReason)
            .MaximumLength(500)
            .NotEmpty()
            .When(x => x.AllowExpiredLotOverride)
            .WithMessage("Expired lot override requires a reason.");
    }
}
