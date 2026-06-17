namespace Community.Api.Features.Content.Edit;

public sealed class EditContentValidator : AbstractValidator<EditContentRequest>
{
    public EditContentValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");

        RuleFor(x => x.Text)
            .MaximumLength(5000).WithMessage("Text cannot exceed 5000 characters")
            .When(x => x.Text is not null);

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters")
            .When(x => x.Title is not null);
    }
}
