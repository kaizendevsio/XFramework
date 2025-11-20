
using Wallets.Api.Features.Batch.IncrementBatch;
using Wallets.Api.Features.Batch.DecrementBatch;
using Wallets.Api.Features.Batch.TransferBatch;

namespace Wallets.Api.Extensions;

public static class ApplicationBuilderExtension
{
    public static WebApplication UseAppServices(this WebApplication app)
    {
        // Register batch operation endpoints
        app.MapBatchIncrementEndpoint();
        app.MapBatchDecrementEndpoint();
        app.MapBatchTransferEndpoint();
        
        return app;
    }
    
}