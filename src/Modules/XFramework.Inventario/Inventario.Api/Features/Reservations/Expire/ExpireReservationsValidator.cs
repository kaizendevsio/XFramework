using FluentValidation;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace Inventario.Api.Features.Reservations.Expire;

public sealed class ExpireReservationsValidator : AbstractValidator<ExpireReservationsRequest>
{
    public ExpireReservationsValidator()
    {
        RuleFor(x => x.MaxCount)
            .InclusiveBetween(1, 500);
    }
}
