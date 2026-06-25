using FluentValidation;

namespace Attendance.Api.Features.Contexts.GetList;

public sealed class GetAttendanceContextsValidator : AbstractValidator<GetAttendanceContextsRequest>
{
    public GetAttendanceContextsValidator()
    {
        RuleFor(x => x.ContextType)
            .IsInEnum().WithMessage("Attendance context type is invalid")
            .When(x => x.ContextType.HasValue);

        RuleFor(x => x.SearchTerm)
            .MaximumLength(200).WithMessage("Search term must not exceed 200 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.SearchTerm));

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than zero");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

