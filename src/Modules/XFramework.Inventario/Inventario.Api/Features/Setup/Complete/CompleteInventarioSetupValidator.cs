using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Setup;

namespace Inventario.Api.Features.Setup.Complete;

public sealed class CompleteInventarioSetupValidator : AbstractValidator<CompleteInventarioSetupRequest>
{
    public CompleteInventarioSetupValidator()
    {
        RuleFor(x => x.CompletionRequestId).NotEmpty().WithMessage("Completion request ID is required.");
        RuleFor(x => x.Mode).IsInEnum().WithMessage("Choose a valid setup mode.");
        RuleFor(x => x.StockAreaName).NotEmpty().MaximumLength(200).When(x => x.UseSimpleStorage);
        RuleFor(x => x).Must(x => !x.UseSimpleStorage ||
            (x.ExistingWarehouseId is null && x.ExistingLocationId is null && x.Warehouse is null && x.Location is null))
            .WithMessage("Choose simple stock setup or explicit storage, not both.");
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).WithMessage("Low stock threshold cannot be negative.");
        RuleFor(x => x.DefaultCurrency).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Default currency is required.")
            .Must(SetupCurrency.IsSupported).WithMessage("Choose a supported currency code.");
    }
}
