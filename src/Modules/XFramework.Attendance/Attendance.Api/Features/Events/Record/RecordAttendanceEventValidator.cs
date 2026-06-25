using FluentValidation;

namespace Attendance.Api.Features.Events.Record;

public sealed class RecordAttendanceEventValidator : AbstractValidator<RecordAttendanceEventRequest>
{
    public RecordAttendanceEventValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Attendance session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("Attendance participant ID is required");

        RuleFor(x => x.EventType)
            .Must(type => type is AttendanceEventType.CheckIn or AttendanceEventType.CheckOut)
            .WithMessage("Attendance event type must be check-in or check-out");

        RuleFor(x => x.Source)
            .IsInEnum().WithMessage("Attendance event source is invalid");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency key is required")
            .MaximumLength(128).WithMessage("Idempotency key must not exceed 128 characters");

        RuleFor(x => x.SourceReference)
            .MaximumLength(256).WithMessage("Source reference must not exceed 256 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.SourceReference));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

