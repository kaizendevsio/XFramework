namespace Community.Api.Features.Content.Files.Create;

public sealed class CreateContentFileValidator : AbstractValidator<CreateContentFileVsaRequest>
{
    public CreateContentFileValidator()
    {
        RuleFor(x => x.ContentId)
            .NotEmpty().WithMessage("Content ID is required");
        RuleFor(x => x.StorageFileId)
            .NotEmpty().WithMessage("Storage File ID is required");
        RuleFor(x => x.RequestingIdentityId)
            .NotEmpty().WithMessage("Requesting Identity ID is required");
    }
}
