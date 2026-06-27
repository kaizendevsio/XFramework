using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Create;
using XFramework.Domain.Shared.Enums;

namespace Communications.Api.Features.Messages.CreateVerification;

public sealed class CreateVerificationMessageValidator : AbstractValidator<CreateVerificationMessageRequest>
{
    public CreateVerificationMessageValidator()
    {
        RuleFor(x => x.VerificationToken)
            .NotEmpty().WithMessage("Verification token is required")
            .MaximumLength(100).WithMessage("Verification token cannot exceed 100 characters");

        RuleFor(x => x.ContactType)
            .IsInEnum().WithMessage("Invalid contact type")
            .NotEqual(GenericContactType.NotSpecified).WithMessage("Contact type is required");

        RuleFor(x => x.Contact)
            .NotEmpty().WithMessage("Contact is required")
            .MaximumLength(320).WithMessage("Contact cannot exceed 320 characters");
    }
}
