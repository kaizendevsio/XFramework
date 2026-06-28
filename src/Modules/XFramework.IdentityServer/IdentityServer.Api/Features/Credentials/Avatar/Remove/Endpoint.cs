using FluentValidation;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Credentials.Avatar.Remove;

public static class RemoveCredentialAvatarEndpoint
{
    [BoltHandler]
    [MapPost("/api/credentials/avatar/remove", Tags = ["Credentials"],
        Summary = "Remove a credential avatar",
        Description = "Clears credential avatar metadata without deleting the stored file.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CredentialAvatarResponse>> Handle(
        RemoveCredentialAvatarRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.RemoveCredentialAvatarAsync(request, ct);
    }
}

public class RemoveCredentialAvatarRequestValidator : AbstractValidator<RemoveCredentialAvatarRequest>
{
    public RemoveCredentialAvatarRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential is required");
    }
}
