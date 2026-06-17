namespace Community.Api.Features.Content.Delete;

public sealed class DeleteContentValidator : AbstractValidator<DeleteContentRequest>
{
    public DeleteContentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Content ID is required");
    }
}
