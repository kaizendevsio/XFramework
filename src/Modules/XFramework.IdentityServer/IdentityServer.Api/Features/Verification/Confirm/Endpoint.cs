using FluentValidation;
using IdentityServer.Api.Features.Verification;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;
using PatchVerificationRequest = XFramework.Domain.Shared.Contracts.Requests.Patch<IdentityServer.Domain.Shared.Contracts.IdentityVerification>;

namespace IdentityServer.Api.Features.Verification.Confirm;

public static class ConfirmVerificationEndpoint
{
    public static IEndpointRouteBuilder MapConfirmVerificationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/verifications/{verificationId:guid}/confirm", ConfirmFromBody)
            .WithTags("Verification")
            .WithSummary("Confirm a verification")
            .WithDescription("Updates a verification status from Pending to Approved when a valid, non-expired token is provided.")
            .Produces<VerificationAdministrationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("verification");

        return app;
    }

    public static async Task<Result<VerificationAdministrationResponse>> Handle(
        PatchVerificationRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return VerificationResponseMapper.Map(
            await authService.UpdateVerificationAsync(request, ct));
    }

    private static async Task<IResult> ConfirmFromBody(
        Guid verificationId,
        ConfirmVerificationRequest body,
        HttpContext httpContext,
        IValidator<ConfirmVerificationRequest> validator,
        IHttpTrustedInvocationAuthorizer invocationAuthorizer,
        ITrustedInvocationFeatureGate featureGate,
        IAuthService authService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(body, ct);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray()));
        }

        var metadata = new RequestMetadata
        {
            RequestId = Guid.NewGuid(),
            RequestedTenantId = body.TenantId,
            OperationName = nameof(ConfirmVerificationRequest),
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString()
        };
        var invocationResult = await invocationAuthorizer.AuthorizeAsync(
            httpContext.Request.Headers.Authorization.ToString(),
            httpContext.Request.Headers["X-XFramework-Service-Authorization"].ToString(),
            metadata,
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.PublicTenantLookup,
                RequireServiceIdentity = false,
                AllowAnonymous = true
            },
            ct);
        if (!invocationResult.IsSuccess)
            return Results.Problem(detail: invocationResult.Error, statusCode: invocationResult.StatusCode);

        var featureResult = await featureGate.EnsureAllowedAsync(
            "/api/verifications/{verificationId:guid}/confirm",
            HttpMethods.Patch,
            null,
            ct);
        if (!featureResult.IsSuccess)
            return Results.Problem(detail: featureResult.Message, statusCode: featureResult.StatusCode);

        var request = new PatchVerificationRequest(new IdentityVerification
        {
            Id = verificationId,
            Token = body.Token
        });
        var result = await Handle(request, authService, ct);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(detail: result.Message, statusCode: result.StatusCode);
    }
}

public sealed record ConfirmVerificationRequest(string? Token, Guid TenantId);

public sealed class ConfirmVerificationRequestValidator : AbstractValidator<ConfirmVerificationRequest>
{
    public ConfirmVerificationRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("Verification token is required")
            .MaximumLength(2_048).WithMessage("Verification token is too long");
        RuleFor(request => request.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");
    }
}
