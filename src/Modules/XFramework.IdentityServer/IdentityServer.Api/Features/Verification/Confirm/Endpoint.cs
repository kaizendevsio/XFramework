using FluentValidation;
using IdentityServer.Api.Features.Verification;
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
        IValidator<ConfirmVerificationRequest> validator,
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

public sealed record ConfirmVerificationRequest(string? Token);

public sealed class ConfirmVerificationRequestValidator : AbstractValidator<ConfirmVerificationRequest>
{
    public ConfirmVerificationRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("Verification token is required")
            .MaximumLength(2_048).WithMessage("Verification token is too long");
    }
}
