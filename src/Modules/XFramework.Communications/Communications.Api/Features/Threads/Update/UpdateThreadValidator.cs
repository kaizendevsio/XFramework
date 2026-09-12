using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Threads.Update;

public sealed class UpdateThreadValidator : AbstractValidator<UpdateThreadRequest>
{
    public UpdateThreadValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters")
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => x.Description is not null);
        RuleFor(x => x.Features).Must(value => !value.HasValue || (value.Value & ~Communications.Domain.Shared.Contracts.ConversationFeatures.All) == 0)
            .WithMessage("Unknown conversation feature");
        RuleFor(x => x.Nickname).NotNull().MaximumLength(80).When(x => x.NicknameMemberId.HasValue);
    }
}
