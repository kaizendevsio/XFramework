using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Threads;

namespace Messaging.Api.Features.Threads.Get;

public sealed class GetThreadValidator : AbstractValidator<GetThreadRequest>
{
    public GetThreadValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.RequesterCredentialId)
            .NotEmpty().WithMessage("Requester credential ID is required");
    }
}
