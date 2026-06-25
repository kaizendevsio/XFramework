using FluentValidation;

namespace Attendance.Api.Features.Reports.GetContextRange;

public sealed class GetAttendanceReportValidator : AbstractValidator<GetAttendanceReportRequest>
{
    public GetAttendanceReportValidator()
    {
        RuleFor(x => x.ContextId)
            .NotEmpty().WithMessage("Attendance context ID is required");

        RuleFor(x => x.FromUtc)
            .NotEmpty().WithMessage("From UTC is required");

        RuleFor(x => x.ToUtc)
            .GreaterThan(x => x.FromUtc).WithMessage("To UTC must be after From UTC");

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than zero");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

