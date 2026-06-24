using FluentValidation;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Credentials.Create;

public static class CreateCredentialEndpoint
{
    [BoltHandler]
    [MapPost("/api/credentials", Tags = ["Credentials"],
        Summary = "Create a new identity credential",
        Description = "Creates a new identity credential with BCrypt password hashing (workFactor 11).",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<IdentityCredential>> Handle(
        CreateCredentialRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        if (request.Metadata.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            return Result<IdentityCredential>.Failure("Tenant context is required.");
        }

        var credential = new IdentityCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentityInfoId = request.IdentityInfoId,
            UserName = request.UserName,
            UserAlias = request.UserAlias,
            Password = request.Password,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        return await authService.CreateCredentialAsync(
            new Create<IdentityCredential>(credential)
            {
                Metadata = request.Metadata
            },
            ct);
    }
}

public class CreateCredentialRequestValidator : AbstractValidator<CreateCredentialRequest>
{
    public CreateCredentialRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.IdentityInfoId)
            .NotEmpty().WithMessage("Identity Info ID is required");
    }
}
