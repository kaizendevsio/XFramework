using Communications.Domain.Shared.Contracts.Requests.Attachments;
using FluentValidation;

namespace Communications.Api.Features.Messages.Attachments.CreateUpload;

public sealed class CreateChatAttachmentUploadValidator : AbstractValidator<CreateChatAttachmentUploadRequest>
{
    public CreateChatAttachmentUploadValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(255);
        RuleFor(x => x.TotalSizeBytes).GreaterThan(0);
        RuleFor(x => x.ChunkSizeBytes).InclusiveBetween(1, 100 * 1024 * 1024).When(x => x.ChunkSizeBytes.HasValue);
        RuleFor(x => x.ExpectedSha256Hash).Must(value => string.IsNullOrWhiteSpace(value) ||
            value.Trim().Length == 64 && value.Trim().All(Uri.IsHexDigit));
    }
}
