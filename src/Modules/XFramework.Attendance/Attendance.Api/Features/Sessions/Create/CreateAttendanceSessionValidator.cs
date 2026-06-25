using FluentValidation;

namespace Attendance.Api.Features.Sessions.Create;

public sealed class CreateAttendanceSessionValidator : AbstractValidator<CreateAttendanceSessionRequest>
{
    public CreateAttendanceSessionValidator()
    {
        RuleFor(x => x.ContextId)
            .NotEmpty().WithMessage("Attendance context ID is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Attendance session name is required")
            .MaximumLength(200).WithMessage("Attendance session name must not exceed 200 characters");

        RuleFor(x => x.Code)
            .MaximumLength(64).WithMessage("Attendance session code must not exceed 64 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("Time zone ID is required")
            .MaximumLength(100).WithMessage("Time zone ID must not exceed 100 characters");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Attendance session status is invalid");

        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt).WithMessage("Attendance session end must be after start");
    }
}

