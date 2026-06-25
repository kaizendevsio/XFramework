using FluentValidation;

namespace Attendance.Api.Features.Participants.Add;

public sealed class AddAttendanceParticipantValidator : AbstractValidator<AddAttendanceParticipantRequest>
{
    public AddAttendanceParticipantValidator()
    {
        RuleFor(x => x.ContextId)
            .NotEmpty().WithMessage("Attendance context ID is required");

        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.DisplayName)
            .MaximumLength(200).WithMessage("Display name must not exceed 200 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName));

        RuleFor(x => x.ReferenceCode)
            .MaximumLength(128).WithMessage("Reference code must not exceed 128 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ReferenceCode));
    }
}

