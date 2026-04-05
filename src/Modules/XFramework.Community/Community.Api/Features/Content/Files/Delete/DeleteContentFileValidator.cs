namespace Community.Api.Features.Content.Files.Delete;

public sealed class DeleteContentFileValidator : AbstractValidator<DeleteContentFileRequest>
{
    public DeleteContentFileValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required");
        RuleFor(x => x.RequestingIdentityId)
            .NotEmpty().WithMessage("Requesting Identity ID is required");
    }
}
