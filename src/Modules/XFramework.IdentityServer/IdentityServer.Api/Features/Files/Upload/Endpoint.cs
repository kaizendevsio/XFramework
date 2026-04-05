using FluentValidation;
using XFramework.Integration.Attributes;
using CreateFileRequest = XFramework.Domain.Shared.Contracts.Requests.Create<XFramework.Domain.Shared.Contracts.StorageFile>;

namespace IdentityServer.Api.Features.Files.Upload;

public static class UploadFileEndpoint
{
    [MapPost("/api/files", Tags = ["Files"],
        Summary = "Upload a file",
        Description = "Uploads a file to Azure Blob Storage.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<StorageFile>> Handle(
        CreateFileRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.CreateFileAsync(request, ct);
    }
}

public class UploadFileRequestValidator : AbstractValidator<CreateFileRequest>
{
    public UploadFileRequestValidator()
    {
        RuleFor(x => x.Model.FileBytes)
            .NotEmpty().WithMessage("File bytes are required");

        RuleFor(x => x.Model.TypeId)
            .NotEmpty().WithMessage("File type ID is required");

        RuleFor(x => x.Model.StorageFileIdentifierId)
            .NotEmpty().WithMessage("Storage file identifier ID is required");

        RuleFor(x => x.Model.BlobContainer)
            .NotEmpty().WithMessage("Blob container name is required");

        RuleFor(x => x.Model.ContentPath)
            .NotEmpty().WithMessage("Content path is required");

        RuleFor(x => x.Model.ContentType)
            .NotEmpty().WithMessage("Content type is required");
    }
}
