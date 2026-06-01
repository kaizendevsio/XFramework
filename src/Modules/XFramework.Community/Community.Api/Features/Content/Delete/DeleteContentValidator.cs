namespace Community.Api.Features.Content.Delete;

public sealed class DeleteContentValidator : AbstractValidator<DeleteContentRequest>
{
    public DeleteContentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Content ID is required");

        RuleFor(x => x.RequesterId)
            .NotEmpty().WithMessage("Requester ID is required");
    }
}
