using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http.HttpResults;

namespace IdentityServer.Api.Features.Auth.Authenticate;

/// <summary>
/// Authenticate endpoint - Multi-type authentication (Username/Email/Phone/Token)
/// </summary>
public static class AuthenticateEndpoint
{
    public static void MapAuthenticate(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/authenticate", Handle)
            .WithName("Authenticate")
            .WithTags("Auth")
            .WithOpenApi(op =>
            {
                op.Summary = "Authenticate a user";
                op.Description = "Authenticates a user with multi-type support (Username, Email, Phone, Token). Generates JWT tokens and creates session.";
                return op;
            })
            .ExcludeFromDescription(); // Workaround: dotnet/aspnetcore#63857 — response contains IdentityCredential
    }

    private static async Task<Results<Ok<AuthenticateIdentityResponse>, NotFound, UnauthorizedHttpResult, ForbidHttpResult, ProblemHttpResult>> Handle(
        AuthenticateIdentityRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        var result = await authService.AuthenticateAsync(request, ct);

        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                401 => TypedResults.Unauthorized(),
                403 => TypedResults.Forbid(),
                404 => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    title: "Authentication failed",
                    detail: result.Message,
                    statusCode: result.StatusCode
                )
            };
        }

        return TypedResults.Ok(result.Data!);
    }
}