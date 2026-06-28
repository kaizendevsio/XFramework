using FluentValidation;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Credentials.Avatar.Set;

public static class SetCredentialAvatarEndpoint
{
    [BoltHandler]
    [MapPost("/api/credentials/avatar/set", Tags = ["Credentials"],
        Summary = "Set a credential avatar",
        Description = "Attaches an existing image storage file as the credential avatar.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CredentialAvatarResponse>> Handle(
        SetCredentialAvatarRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.SetCredentialAvatarAsync(request, ct);
    }
}

public class SetCredentialAvatarRequestValidator : AbstractValidator<SetCredentialAvatarRequest>
{
    public SetCredentialAvatarRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential is required");

        RuleFor(x => x.StorageFileId)
            .NotEmpty().WithMessage("Storage file is required");
    }
}
