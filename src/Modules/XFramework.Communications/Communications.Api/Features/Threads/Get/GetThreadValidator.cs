using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Threads.Get;

public sealed class GetThreadValidator : AbstractValidator<GetThreadRequest>
{
    public GetThreadValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Thread ID is required");
    }
}
