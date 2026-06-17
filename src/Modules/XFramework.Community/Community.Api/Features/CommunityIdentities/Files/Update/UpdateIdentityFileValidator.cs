namespace Community.Api.Features.CommunityIdentities.Files.Update;

public sealed class UpdateIdentityFileValidator : AbstractValidator<UpdateIdentityFileRequest>
{
    public UpdateIdentityFileValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required");
        RuleFor(x => x.StorageFileId)
            .NotEmpty().WithMessage("Storage File ID is required");
    }
}
