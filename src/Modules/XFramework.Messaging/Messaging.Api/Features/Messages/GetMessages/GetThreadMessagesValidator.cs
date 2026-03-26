using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Threads;

namespace Messaging.Api.Features.Messages.GetMessages;

public sealed class GetThreadMessagesValidator : AbstractValidator<GetThreadMessagesRequest>
{
    public GetThreadMessagesValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.RequesterCredentialId)
            .NotEmpty().WithMessage("Requester credential ID is required");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100")
            .When(x => x.PageSize != 0);
    }
}
