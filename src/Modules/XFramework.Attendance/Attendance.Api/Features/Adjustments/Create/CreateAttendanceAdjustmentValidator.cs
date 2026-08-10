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

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Adjustment reason is required")
            .MaximumLength(500).WithMessage("Adjustment reason must not exceed 500 characters");

        RuleFor(x => x.AdjustedCheckInAt)
            .Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc)
            .WithMessage("Adjusted check-in time must be UTC");

        RuleFor(x => x.AdjustedCheckOutAt)
            .Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc)
            .WithMessage("Adjusted checkout time must be UTC")
            .Must((request, checkOut) => !checkOut.HasValue || request.AdjustedCheckInAt.HasValue)
            .WithMessage("Adjusted checkout requires an adjusted check-in")
            .GreaterThanOrEqualTo(x => x.AdjustedCheckInAt)
            .When(x => x.AdjustedCheckInAt.HasValue && x.AdjustedCheckOutAt.HasValue)
            .WithMessage("Adjusted checkout cannot be before adjusted check-in");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

