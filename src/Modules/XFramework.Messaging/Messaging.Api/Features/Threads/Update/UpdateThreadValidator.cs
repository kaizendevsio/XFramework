using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Threads;

namespace Messaging.Api.Features.Threads.Update;

public sealed class UpdateThreadValidator : AbstractValidator<UpdateThreadRequest>
{
    public UpdateThreadValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.RequesterCredentialId)
            .NotEmpty().WithMessage("Requester Credential ID is required");

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters")
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => x.Description is not null);
    }
}
