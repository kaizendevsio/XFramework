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
            .MaximumLength(100).WithMessage("Time zone ID must not exceed 100 characters")
            .Must(BeValidTimeZone).WithMessage("Time zone ID is invalid");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Attendance session status is invalid")
            .Must(status => status is AttendanceSessionStatus.Scheduled or AttendanceSessionStatus.Open)
            .WithMessage("New attendance sessions must be scheduled or open");

        RuleFor(x => x.StartsAt)
            .Must(BeUtc).WithMessage("Attendance session start must be UTC");

        RuleFor(x => x.EndsAt)
            .Must(BeUtc).WithMessage("Attendance session end must be UTC")
            .GreaterThan(x => x.StartsAt).WithMessage("Attendance session end must be after start");
    }

    private static bool BeUtc(DateTime value) => value.Kind == DateTimeKind.Utc;

    private static bool BeValidTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return false;

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

