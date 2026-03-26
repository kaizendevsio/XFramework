using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Threads;

namespace Messaging.Api.Features.Threads.Members.Remove;

public sealed class RemoveThreadMemberValidator : AbstractValidator<RemoveThreadMemberRequest>
{
    public RemoveThreadMemberValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");
    }
}
