using FluentValidation;

namespace Attendance.Api.Features.Participants.GetList;

public sealed class GetAttendanceParticipantsValidator : AbstractValidator<GetAttendanceParticipantsRequest>
{
    public GetAttendanceParticipantsValidator()
    {
        RuleFor(x => x.ContextId)
            .NotEmpty().WithMessage("Attendance context ID is required");

        RuleFor(x => x.SearchTerm)
            .MaximumLength(200).WithMessage("Search term must not exceed 200 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.SearchTerm));

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than zero");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

