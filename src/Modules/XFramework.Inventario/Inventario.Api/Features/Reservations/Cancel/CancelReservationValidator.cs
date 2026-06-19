using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Cancel;

public sealed class CancelReservationValidator : AbstractValidator<CancelReservationRequest>
{
    public CancelReservationValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty();

        RuleFor(x => x.Reason)
            .MaximumLength(500);
    }
}
