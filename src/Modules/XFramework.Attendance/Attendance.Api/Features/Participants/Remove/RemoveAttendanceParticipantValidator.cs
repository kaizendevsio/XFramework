using FluentValidation;

namespace Attendance.Api.Features.Participants.Remove;

public sealed class RemoveAttendanceParticipantValidator : AbstractValidator<RemoveAttendanceParticipantRequest>
{
    public RemoveAttendanceParticipantValidator()
    {
        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("Attendance participant ID is required");
    }
}

