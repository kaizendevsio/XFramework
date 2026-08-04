using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.Services.FeatureGates;

namespace IdentityServer.Api.Features.Credentials.Update;

public static class UpdateCredentialEndpoint
{
    public static IEndpointRouteBuilder MapUpdateCredentialEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/credentials/{id:guid}", Handle)
            .WithName("UpdateCredential")
            .WithTags("Credentials")
            .WithSummary("Update an identity credential")
            .WithDescription("Updates an identity credential (excluding password, use change-password endpoint for that).")
            .RequireAuthorization()
            .WithMetadata(new TenantCapabilityRequirement(IdentityAuthorizationConstants.Update))
            .Produces<CredentialAdministrationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<Results<Ok<CredentialAdministrationResponse>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        Guid id,
        UpdateCredentialRequest command,
        HttpContext httpContext,
        [FromServices] IValidator<UpdateCredentialRequest> validator,
        [FromServices] IHttpTrustedInvocationAuthorizer invocationAuthorizer,
        [FromServices] ITrustedInvocationFeatureGate featureGate,
        [FromServices] IActorAccessTokenScope actorAccessTokenScope,
        [FromServices] IAuthService authService,
        CancellationToken ct)
    {
        command.Metadata ??= new RequestMetadata();
        var authorizationHeader = httpContext.Request.Headers.Authorization.ToString();
        var actorAccessToken = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[7..].Trim()
            : null;
        using var actorTokenLease = string.IsNullOrWhiteSpace(actorAccessToken)
            ? null
            : actorAccessTokenScope.Push(actorAccessToken);
        var invocationAuthorization = await invocationAuthorizer.AuthorizeAsync(
            authorizationHeader,
            httpContext.Request.Headers["X-XFramework-Service-Authorization"].ToString(),
            command.Metadata,
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Required,
                TenantAccessMode = TenantAccessMode.ActorTenant,
                RequireServiceIdentity = false
            },
            ct);
        if (!invocationAuthorization.IsSuccess)
        {
            return TypedResults.Problem(
                detail: invocationAuthorization.Error,
                statusCode: invocationAuthorization.StatusCode);
        }

        var featureResult = await featureGate.EnsureAllowedAsync(
            "/api/credentials/{id:guid}",
            HttpMethods.Patch,
            IdentityAuthorizationConstants.Update,
            ct);
        if (!featureResult.IsSuccess)
        {
            return TypedResults.Problem(
                detail: featureResult.Message,
                statusCode: featureResult.StatusCode);
        }

        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(static error => error.PropertyName)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Select(static error => error.ErrorMessage).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        if (command.CredentialId is { } bodyCredentialId && bodyCredentialId != id)
        {
            return TypedResults.Problem(
                detail: "Route credential ID does not match the request body",
                statusCode: StatusCodes.Status400BadRequest);
        }

        command.CredentialId = id;
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(command.Metadata, httpContext);
        var result = await authService.UpdateCredentialAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.StatusCode switch
            {
                StatusCodes.Status404NotFound => TypedResults.NotFound(),
                _ => TypedResults.Problem(detail: result.Message, statusCode: result.StatusCode)
            };
        }

        return TypedResults.Ok(result.Data!);
    }
}

public class UpdateCredentialRequestValidator : AbstractValidator<UpdateCredentialRequest>
{
    public UpdateCredentialRequestValidator()
    {
        RuleFor(request => request.UserName)
            .NotEmpty()
            .MaximumLength(256)
            .When(request => request.UserName is not null);

        RuleFor(request => request.UserAlias)
            .NotEmpty()
            .MaximumLength(256)
            .When(request => request.UserAlias is not null);

        RuleFor(request => request.ExpectedConcurrencyStamp)
            .NotEmpty()
            .WithMessage("Expected concurrency stamp is required");

        RuleFor(request => request)
            .Must(request => request.UserName is not null ||
                             request.UserAlias is not null ||
                             request.IsEnabled.HasValue)
            .WithMessage("At least one credential field must be supplied");
    }
}
