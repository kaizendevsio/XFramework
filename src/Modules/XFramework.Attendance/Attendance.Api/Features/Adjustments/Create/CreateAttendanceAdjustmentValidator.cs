using FluentValidation;

namespace Attendance.Api.Features.Adjustments.Create;

public sealed class CreateAttendanceAdjustmentValidator : AbstractValidator<CreateAttendanceAdjustmentRequest>
{
    public CreateAttendanceAdjustmentValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Attendance session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("Attendance participant ID is required");

        RuleFor(x => x.NewStatus)
            .IsInEnum().WithMessage("Attendance status is invalid")
            .Must(status => status != AttendanceRecordStatus.Unknown)
            .WithMessage("Attendance status must be explicit");

        RuleFor(x => x.ActorCredentialId)
            .NotEmpty().WithMessage("Actor credential ID is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Adjustment reason is required")
            .MaximumLength(500).WithMessage("Adjustment reason must not exceed 500 characters");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

