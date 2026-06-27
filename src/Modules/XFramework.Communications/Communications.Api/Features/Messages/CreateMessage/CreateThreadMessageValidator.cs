using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Messages.CreateMessage;

public sealed class CreateThreadMessageValidator : AbstractValidator<CreateThreadMessageRequest>
{
    public CreateThreadMessageValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.Text)
            .MaximumLength(5000).WithMessage("Message text cannot exceed 5000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Text));

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Text) || x.TemplateId is not null || !string.IsNullOrWhiteSpace(x.TemplateKey))
            .WithMessage("Message text or template is required");

        RuleFor(x => x.TemplateKey)
            .MaximumLength(128).WithMessage("Template key cannot exceed 128 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.TemplateKey));
    }
}
