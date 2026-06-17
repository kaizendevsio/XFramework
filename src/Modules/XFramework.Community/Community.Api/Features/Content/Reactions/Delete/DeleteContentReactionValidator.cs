namespace Community.Api.Features.Content.Reactions.Delete;

public sealed class DeleteContentReactionValidator : AbstractValidator<DeleteContentReactionRequest>
{
    public DeleteContentReactionValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");

        RuleFor(x => x.ReactionId)
            .NotEmpty().WithMessage("Reaction ID is required");
    }
}
