using FluentValidation;
using IdentityServer.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using CreateFileRequest = XFramework.Domain.Shared.Contracts.Requests.Create<XFramework.Domain.Shared.Contracts.StorageFile>;

namespace IdentityServer.Api.Features.Files.Upload;

/// <summary>
/// Upload file endpoint - Uploads file to Azure Blob Storage
/// </summary>
public static class UploadFileEndpoint
{
    public static void MapUploadFile(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/files", Handle)
            .WithName("UploadFile")
            .WithTags("Files")
            .WithOpenApi(op =>
            {
                op.Summary = "Upload a file";
                op.Description = "Uploads a file to Azure Blob Storage.";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857
    }

    private static async Task<Results<Created<StorageFile>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        CreateFileRequest request,
        IAuthService authService,
        IValidator<CreateFileRequest> validator,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );
            
            return TypedResults.ValidationProblem(errors);
        }

        var result = await authService.CreateFileAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    title: "Error uploading file",
                    detail: result.Message,
                    statusCode: result.StatusCode
                )
            };
        }

        return TypedResults.Created($"/api/files/{result.Data!.Id}", result.Data);
    }
}

/// <summary>
/// Validator for Create StorageFile request
/// </summary>
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