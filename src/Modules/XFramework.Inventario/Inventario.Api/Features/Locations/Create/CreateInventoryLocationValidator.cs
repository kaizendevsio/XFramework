using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;
using XFramework.Inventario.Domain.Shared.Enums;

namespace Inventario.Api.Features.Locations.Create;

public sealed class CreateInventoryLocationValidator : AbstractValidator<CreateInventoryLocationRequest>
{
    public CreateInventoryLocationValidator()
    {
        RuleFor(x => x.WarehouseId)
            .NotEmpty();

        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.LocationType)
            .Must(value => Enum.IsDefined(typeof(InventoryLocationType), value))
            .WithMessage("Location type is invalid.");
    }
}
