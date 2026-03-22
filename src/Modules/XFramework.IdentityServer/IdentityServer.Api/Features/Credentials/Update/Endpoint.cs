using FluentValidation;
using IdentityServer.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Attributes;
using PatchRequest = XFramework.Domain.Shared.Contracts.Requests.Patch<IdentityServer.Domain.Shared.Contracts.IdentityCredential>;

namespace IdentityServer.Api.Features.Credentials.Update;

public static class UpdateCredentialEndpoint
{
    [MapPatch("/api/credentials/{id:guid}", Tags = ["Credentials"],
        Summary = "Update an identity credential",
        Description = "Updates an identity credential (excluding password, use change-password endpoint for that).",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<IdentityCredential>> Handle(
        PatchRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.UpdateCredentialAsync(request, ct);
    }
}

public class UpdateCredentialRequestValidator : AbstractValidator<PatchRequest>
{
    public UpdateCredentialRequestValidator()
    {
        RuleFor(x => x.Model.Id)
            .NotEmpty().WithMessage("Credential ID is required");
    }
}
