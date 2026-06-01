using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Threads;

namespace Messaging.Api.Features.Threads.Create;

public sealed class CreateThreadValidator : AbstractValidator<CreateThreadRequest>
{
    public CreateThreadValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Thread name is required")
            .MaximumLength(200).WithMessage("Thread name cannot exceed 200 characters");

        RuleFor(x => x.TypeId)
            .NotEmpty().WithMessage("Thread type ID is required");

        RuleFor(x => x.InitialMemberCredentialIds)
            .NotEmpty().WithMessage("At least one member is required")
            .Must(ids => ids.Count > 0).WithMessage("At least one member credential ID is required");

        RuleFor(x => x.InitialMemberCredentialIds)
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Member credential IDs cannot be empty")
            .When(x => x.InitialMemberCredentialIds.Count > 0);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
