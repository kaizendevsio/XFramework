using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Messages.MarkDelivered;

public sealed class MarkMessagesDeliveredValidator : AbstractValidator<MarkMessagesDeliveredRequest>
{
    public MarkMessagesDeliveredValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.MessageIds).NotEmpty().Must(ids => ids is { Count: <= 50 });
        RuleForEach(x => x.MessageIds).NotEmpty();
    }
}
