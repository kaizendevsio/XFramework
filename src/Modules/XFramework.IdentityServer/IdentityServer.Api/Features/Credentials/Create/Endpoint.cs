using FluentValidation;
using XFramework.Integration.Attributes;
using CreateRequest = XFramework.Domain.Shared.Contracts.Requests.Create<IdentityServer.Domain.Shared.Contracts.IdentityCredential>;

namespace IdentityServer.Api.Features.Credentials.Create;

public static class CreateCredentialEndpoint
{
    [MapPost("/api/credentials", Tags = ["Credentials"],
        Summary = "Create a new identity credential",
        Description = "Creates a new identity credential with BCrypt password hashing (workFactor 11).",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<IdentityCredential>> Handle(
        CreateRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.CreateCredentialAsync(request, ct);
    }
}

public class CreateCredentialRequestValidator : AbstractValidator<CreateRequest>
{
    public CreateCredentialRequestValidator()
    {
        RuleFor(x => x.Model.UserName)
            .NotEmpty().WithMessage("Username is required");

        RuleFor(x => x.Model.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.Model.IdentityInfoId)
            .NotEmpty().WithMessage("Identity Info ID is required");
    }
}
