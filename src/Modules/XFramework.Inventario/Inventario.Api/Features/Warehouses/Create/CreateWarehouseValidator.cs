using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;

namespace Inventario.Api.Features.Warehouses.Create;

public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouseRequest>
{
    public CreateWarehouseValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.AddressLine)
            .MaximumLength(500);

        RuleFor(x => x.City)
            .MaximumLength(100);

        RuleFor(x => x.Region)
            .MaximumLength(100);

        RuleFor(x => x.PostalCode)
            .MaximumLength(30);

        RuleFor(x => x.CountryCode)
            .Length(2)
            .When(x => !string.IsNullOrWhiteSpace(x.CountryCode))
            .WithMessage("Country code must use the two-character ISO code.");
    }
}
