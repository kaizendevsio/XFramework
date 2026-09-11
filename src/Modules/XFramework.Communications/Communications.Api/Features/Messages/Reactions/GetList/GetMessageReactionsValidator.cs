using Communications.Domain.Shared.Contracts.Requests.Reactions;
using FluentValidation;

namespace Communications.Api.Features.Messages.Reactions.GetList;

public sealed class GetMessageReactionsValidator : AbstractValidator<GetMessageReactionsRequest>
{
    public GetMessageReactionsValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.PageIndex).InclusiveBetween(0, 100);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
