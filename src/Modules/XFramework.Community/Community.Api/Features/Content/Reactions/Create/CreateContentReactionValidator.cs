using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Content.Reactions.Create;

/// <summary>
/// Validator for CreateContentReactionRequest
/// </summary>
public sealed class CreateContentReactionValidator : AbstractValidator<CreateContentReactionRequest>
{
    public CreateContentReactionValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");

        RuleFor(x => x.TypeId)
            .NotEmpty().WithMessage("Reaction type ID is required");

        RuleFor(x => x.IdentityId)
            .NotEmpty().WithMessage("Identity ID is required");
    }
}
