using Coins.Api.Features.Blockchain.Send;

namespace Coins.Api.Features.Blockchain;

/// <summary>
/// Endpoint mappings for Blockchain operations
/// </summary>
public static class BlockchainEndpoints
{
    public static void MapBlockchainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Blockchain")
            .WithTags("Blockchain")
            .WithOpenApi();

        // POST /Blockchain/Send - Bulk send Bitcoin transactions
        group.MapPost("/Send", SendEndpoint.HandleAsync)
            .WithName("SendTransactions")
            .WithDescription("Execute bulk send operation to multiple Bitcoin addresses");
    }
}