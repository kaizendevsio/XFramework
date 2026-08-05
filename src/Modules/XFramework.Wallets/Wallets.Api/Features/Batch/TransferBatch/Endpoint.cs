using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Security;

namespace Wallets.Api.Features.Batch.TransferBatch;

/// <summary>
/// Endpoint for batch wallet transfer operations
/// </summary>
public static class Endpoint
{
    /// <summary>
    /// Processes a batch of wallet transfer requests
    /// </summary>
    /// <param name="request">The batch transfer request wrapper</param>
    /// <param name="batchService">Batch wallet service</param>
    /// <param name="contextResolver">Wallet request context resolver</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation result</returns>
    public static async Task<IResult> HandleAsync(
        [FromBody] BatchTransferRequestWrapper request,
        HttpContext httpContext,
        [FromServices] IBatchWalletService batchService,
        [FromServices] IHttpTrustedInvocationAuthorizer invocationAuthorizer,
        [FromServices] ITrustedInvocationFeatureGate featureGate,
        [FromServices] IWalletRequestContextResolver contextResolver,
        CancellationToken cancellationToken = default)
    {
        if (request?.Requests == null || request.Requests.Count == 0)
        {
            return Results.BadRequest(new { error = "Request list cannot be empty" });
        }

        var invocationResult = await invocationAuthorizer.AuthorizeAsync(
            httpContext.Request.Headers.Authorization.ToString(),
            httpContext.Request.Headers["X-XFramework-Service-Authorization"].ToString(),
            request.Metadata,
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Required,
                TenantAccessMode = TenantAccessMode.ActorTenant,
                RequireServiceIdentity = false,
                RequiredActorCapabilities = [WalletAuthorizationCapabilities.Transact]
            },
            cancellationToken);
        if (!invocationResult.IsSuccess)
            return Results.Problem(detail: invocationResult.Error, statusCode: invocationResult.StatusCode);

        var featureResult = await featureGate.EnsureAllowedAsync(
            "/api/wallets/batch/transfer",
            HttpMethods.Post,
            null,
            cancellationToken);
        if (!featureResult.IsSuccess)
            return Results.Problem(detail: featureResult.Message, statusCode: featureResult.StatusCode);

        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
        {
            return Results.Problem(
                detail: contextResult.Message,
                statusCode: contextResult.StatusCode);
        }

        // Process batch
        var result = await batchService.BatchTransferAsync(
            request.Requests,
            contextResult.Data!,
            request.AllowPartialSuccess,
            request.IdempotencyKey,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Message,
                statusCode: result.StatusCode);
        }

        // Return appropriate status based on results
        if (result.Data!.AllSucceeded)
        {
            return Results.Ok(result.Data);
        }
        
        if (result.Data.AnySucceeded)
        {
            // Partial success - return 207 Multi-Status
            return Results.Json(result.Data, statusCode: 207);
        }

        // All failed
        return Results.BadRequest(result.Data);
    }

    /// <summary>
    /// Registers the batch transfer endpoint
    /// </summary>
    public static IEndpointRouteBuilder MapBatchTransferEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wallets/batch/transfer", HandleAsync)
            .WithName("BatchTransferWallets")
            .WithTags("Wallets", "Batch")
            .WithSummary("Batch transfer between wallets")
            .WithDescription("Processes multiple wallet transfers in a single optimized transaction. " +
                "Each transfer creates debit and credit transactions. Validates balances and wallet existence. " +
                "Performance: 20-50x faster than individual operations.")
            .RequireAuthorization()
            .Produces<BatchOperationResult>(StatusCodes.Status200OK)
            .Produces<BatchOperationResult>(StatusCodes.Status207MultiStatus)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return app;
    }
}

/// <summary>
/// Wrapper for batch transfer requests
/// </summary>
public record BatchTransferRequestWrapper : RequestBase
{
    /// <summary>
    /// List of transfer requests to process
    /// </summary>
    public List<BatchTransferRequest> Requests { get; set; } = new();

    /// <summary>
    /// If true, continues processing after failures; if false, rolls back entire batch on any failure
    /// </summary>
    public bool AllowPartialSuccess { get; set; }

    public string? IdempotencyKey { get; set; }
}
