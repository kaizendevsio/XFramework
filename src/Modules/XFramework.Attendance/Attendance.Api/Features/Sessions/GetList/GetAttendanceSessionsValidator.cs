using FluentValidation;

namespace Attendance.Api.Features.Sessions.GetList;

public sealed class GetAttendanceSessionsValidator : AbstractValidator<GetAttendanceSessionsRequest>
{
    public GetAttendanceSessionsValidator()
    {
        RuleFor(x => x.ContextId)
            .NotEmpty().WithMessage("Attendance context ID is required");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Attendance session status is invalid")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.FromUtc)
            .Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc)
            .WithMessage("From UTC must be UTC");

        RuleFor(x => x.ToUtc)
            .Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc)
            .WithMessage("To UTC must be UTC");

        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc!.Value).WithMessage("To UTC must be after From UTC")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than zero");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

