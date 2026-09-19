using Communications.Domain.Shared.Contracts.Requests.Receipts;
using FluentValidation;

namespace Communications.Api.Features.Messages.Receipts.GetList;

public sealed class GetMessageReceiptsValidator : AbstractValidator<GetMessageReceiptsRequest>
{
    public GetMessageReceiptsValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.PageIndex).InclusiveBetween(0, 100);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
