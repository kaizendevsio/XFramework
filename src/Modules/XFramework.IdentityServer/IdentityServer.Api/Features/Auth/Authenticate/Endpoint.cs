using FluentValidation;
using IdentityServer.Api.Infrastructure;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.Authenticate;

public static class AuthenticateEndpoint
{
    [BoltHandler]
    [MapPost("/api/auth/authenticate", Tags = ["Auth"],
        Summary = "Authenticate a user",
        Description = "Authenticates a user with multi-type support (Username, Email, Phone, Token). Generates JWT tokens and creates session.",
        RateLimitPolicy = "auth",
        ExcludeFromOpenApi = false)]
    public static async Task<Result<AuthenticateIdentityResponse>> Handle(
        AuthenticateIdentityRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.AuthenticateAsync(request, ct);
    }
}

public class AuthenticateIdentityRequestValidator : AbstractValidator<AuthenticateIdentityRequest>
{
    public AuthenticateIdentityRequestValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role ID is required");

        RuleFor(x => x.AuthorizationType)
            .IsInEnum().WithMessage("Authorization type is invalid")
            .NotEqual(AuthorizationType.Token)
            .WithMessage("Service token authentication is not supported by the user authentication endpoint");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required")
            .MaximumLength(320).WithMessage("Username must not exceed 320 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .Must(IdentityPasswordPolicy.IsWithinBcryptByteLimit)
            .WithMessage("Password must not exceed 72 UTF-8 bytes");
    }
}
