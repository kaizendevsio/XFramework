using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
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
