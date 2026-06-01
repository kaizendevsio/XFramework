namespace Community.Api.Features.Content.Get;

public sealed class GetContentValidator : AbstractValidator<GetContentRequest>
{
    public GetContentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Content ID is required");
    }
}
