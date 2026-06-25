using FluentValidation;

namespace Attendance.Api.Features.Contexts.Update;

public sealed class UpdateAttendanceContextValidator : AbstractValidator<UpdateAttendanceContextRequest>
{
    public UpdateAttendanceContextValidator()
    {
        RuleFor(x => x.ContextId)
            .NotEmpty().WithMessage("Attendance context ID is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Attendance context name is required")
            .MaximumLength(200).WithMessage("Attendance context name must not exceed 200 characters");

        RuleFor(x => x.Code)
            .MaximumLength(64).WithMessage("Attendance context code must not exceed 64 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Attendance context description must not exceed 1000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.ContextType)
            .IsInEnum().WithMessage("Attendance context type is invalid");
    }
}

