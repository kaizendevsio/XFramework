using Wallets.Api.Features.Batch.DecrementBatch;
using Wallets.Api.Features.Batch.IncrementBatch;
using Wallets.Api.Features.Batch.TransferBatch;

namespace Wallets.Api.Features.Batch;

/// <summary>
/// Extension methods for registering Batch Wallet endpoints
/// </summary>
public static class BatchEndpoints
{
    /// <summary>
    /// Maps all Batch Wallet endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapBatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wallets/batch")
            .WithTags("Batch")
            .WithOpenApi();

        // Map individual batch endpoints
        app.MapBatchIncrementEndpoint();
        app.MapBatchDecrementEndpoint();
        app.MapBatchTransferEndpoint();

        return app;
    }
}