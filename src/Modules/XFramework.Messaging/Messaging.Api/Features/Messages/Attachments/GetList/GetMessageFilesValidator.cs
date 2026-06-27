using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Attachments;

namespace Messaging.Api.Features.Messages.Attachments.GetList;

public sealed class GetMessageFilesValidator : AbstractValidator<GetMessageFilesRequest>
{
    public GetMessageFilesValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Page index cannot be negative");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}
