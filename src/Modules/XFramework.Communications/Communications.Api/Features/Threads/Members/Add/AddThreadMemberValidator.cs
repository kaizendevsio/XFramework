using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Threads.Members.Add;

public sealed class AddThreadMemberValidator : AbstractValidator<AddThreadMemberRequest>
{
    public AddThreadMemberValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");
    }
}
