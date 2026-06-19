using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Fulfill;

public sealed class FulfillReservationValidator : AbstractValidator<FulfillReservationRequest>
{
    public FulfillReservationValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty();

        RuleFor(x => x.Reason)
            .MaximumLength(500);
    }
}
