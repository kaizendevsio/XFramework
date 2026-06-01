using FluentValidation;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.Authenticate;

public static class AuthenticateEndpoint
{
    [BoltHandler]
    [MapPost("/api/auth/authenticate", Tags = ["Auth"],
        Summary = "Authenticate a user",
        Description = "Authenticates a user with multi-type support (Username, Email, Phone, Token). Generates JWT tokens and creates session.",
        ExcludeFromOpenApi = true)]
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
            .IsInEnum().WithMessage("Authorization type is invalid");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}
