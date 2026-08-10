using FluentValidation;

namespace Attendance.Api.Features.Sessions.Transition;

public sealed class TransitionAttendanceSessionValidator : AbstractValidator<TransitionAttendanceSessionRequest>
{
    public TransitionAttendanceSessionValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Attendance session ID is required");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Attendance session status is invalid")
            .Must(status => status is AttendanceSessionStatus.Open or AttendanceSessionStatus.Closed or AttendanceSessionStatus.Cancelled)
            .WithMessage("Attendance session target status must be open, closed, or cancelled");
    }
}
