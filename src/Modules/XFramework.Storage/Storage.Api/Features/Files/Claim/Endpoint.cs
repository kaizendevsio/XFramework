using FluentValidation;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Storage.Api.Features.Files.Claim;

public static class ClaimStorageFileEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.IdentityServer, XFrameworkServiceNames.Portal])]
    [MapPost("/api/storage/files/{storageFileId:guid}/claim", Tags = ["Storage"],
        Summary = "Claim storage file",
        Description = "Idempotently claims a completed file so unclaimed-file maintenance will not delete it.")]
    public static Task<Result<StorageFileResponse>> Handle(
        ClaimStorageFileRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.ClaimFileAsync(request, ct);
}

public sealed class ClaimStorageFileRequestValidator : AbstractValidator<ClaimStorageFileRequest>
{
    public ClaimStorageFileRequestValidator() =>
        RuleFor(request => request.StorageFileId).NotEmpty();
}
