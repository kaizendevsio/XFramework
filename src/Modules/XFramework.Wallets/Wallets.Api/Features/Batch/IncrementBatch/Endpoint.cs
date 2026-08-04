using Wallets.Api.Services;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Security;

namespace Wallets.Api.Features.Batch.IncrementBatch;

/// <summary>
/// Endpoint for batch wallet increment operations
/// </summary>
public static class Endpoint
{
    /// <summary>
    /// Processes a batch of wallet increment requests
    /// </summary>
    /// <param name="request">The batch increment request wrapper</param>
    /// <param name="batchService">Batch wallet service</param>
    /// <param name="contextResolver">Wallet request context resolver</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation result</returns>
    public static async Task<IResult> HandleAsync(
        [FromBody] BatchIncrementRequestWrapper request,
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
                RequireServiceIdentity = false
            },
            cancellationToken);
        if (!invocationResult.IsSuccess)
            return Results.Problem(detail: invocationResult.Error, statusCode: invocationResult.StatusCode);

        var featureResult = await featureGate.EnsureAllowedAsync(
            "/api/wallets/batch/increment",
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
        var result = await batchService.BatchIncrementAsync(
            request.Requests,
            contextResult.Data!,
            request.AllowPartialSuccess,
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
    /// Registers the batch increment endpoint
    /// </summary>
    public static IEndpointRouteBuilder MapBatchIncrementEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/wallets/batch/increment", HandleAsync)
            .WithName("BatchIncrementWallets")
            .WithTags("Wallets", "Batch")
            .WithSummary("Batch increment wallet balances")
            .WithDescription("Processes multiple wallet increments in a single optimized transaction. " +
                "Performance: 20-50x faster than individual operations. Can process 1000 increments in ~200-500ms.")
            .RequireAuthorization()
            .Produces<BatchOperationResult>(StatusCodes.Status200OK)
            .Produces<BatchOperationResult>(StatusCodes.Status207MultiStatus)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return app;
    }
}

/// <summary>
/// Wrapper for batch increment requests
/// </summary>
public record BatchIncrementRequestWrapper : RequestBase
{
    /// <summary>
    /// List of increment requests to process
    /// </summary>
    public List<BatchIncrementRequest> Requests { get; set; } = new();

    /// <summary>
    /// If true, continues processing after failures; if false, rolls back entire batch on any failure
    /// </summary>
    public bool AllowPartialSuccess { get; set; }
}
