using Communications.Domain.Shared.Contracts.Requests.Attachments;
using FluentValidation;

namespace Communications.Api.Features.Messages.Attachments.DownloadUrl;

public sealed class GetChatAttachmentDownloadUrlValidator : AbstractValidator<GetChatAttachmentDownloadUrlRequest>
{
    public GetChatAttachmentDownloadUrlValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.ExpirationMinutes).GreaterThan(0).When(x => x.ExpirationMinutes.HasValue);
    }
}
