using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

namespace Inventario.Api.Features.Purchasing.Receiving;

public sealed class ReceiveInventoryValidator : AbstractValidator<ReceiveInventoryRequest>
{
    public ReceiveInventoryValidator()
    {
        RuleFor(x => x.ReceiptNumber).MaximumLength(100);
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.ReferenceNumber).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).MaximumLength(200);
        RuleFor(x => x.ReceivedAt)
            .Must(value => value is null || value <= DateTime.UtcNow.AddMinutes(5))
            .When(x => x.ReceivedAt is not null)
            .WithMessage("Received date cannot be in the future.");
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.ProductId).NotEmpty();
            line.RuleFor(x => x.Quantity).GreaterThan(0);
            line.RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
            line.RuleFor(x => x.UnitOfMeasure).MaximumLength(25);
            line.RuleFor(x => x.LotNumber).MaximumLength(100);
            line.RuleFor(x => x.SupplierReference).MaximumLength(200);
            line.RuleFor(x => x)
                .Must(x => x.LotId is null || string.IsNullOrWhiteSpace(x.LotNumber))
                .WithMessage("Use either an existing lot or a new lot number, not both.");
            line.RuleFor(x => x.ExpiresAt)
                .GreaterThanOrEqualTo(x => x.ManufacturedAt)
                .When(x => x.ExpiresAt is not null && x.ManufacturedAt is not null);
        });
    }
}
