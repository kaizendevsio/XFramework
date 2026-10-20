using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;

namespace Inventario.Api.Features.Setup.UpdatePreferences;

public sealed class UpdateInventarioPreferencesValidator : AbstractValidator<UpdateInventarioPreferencesRequest>
{
    public UpdateInventarioPreferencesValidator()
    {
        RuleFor(x => x.Mode).IsInEnum().When(x => x.Mode.HasValue);
        RuleFor(x => x).Must(x => x.WarehouseId.HasValue == x.LocationId.HasValue)
            .WithMessage("Select both a stock area and its location.");
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).WithMessage("Low stock threshold cannot be negative.");
        RuleFor(x => x.DefaultCurrency).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Default currency is required.")
            .Must(SetupCurrency.IsSupported).WithMessage("Choose a supported currency code.");
    }
}
