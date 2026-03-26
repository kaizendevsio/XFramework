using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Content.Create;

/// <summary>
/// Validator for CreateContentRequest
/// </summary>
public sealed class CreateContentValidator : AbstractValidator<CreateContentRequest>
{
    public CreateContentValidator()
    {
        RuleFor(x => x.IdentityId)
            .NotEmpty().WithMessage("Identity ID is required");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required")
            .MaximumLength(5000).WithMessage("Text cannot exceed 5000 characters");

        RuleFor(x => x.TypeId)
            .NotEmpty().WithMessage("Type ID is required");
    }
}
