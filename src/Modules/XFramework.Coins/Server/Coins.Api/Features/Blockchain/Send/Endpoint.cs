using Coins.Api.BusinessObjects;
using Coins.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Coins.Api.Features.Blockchain.Send;

public static class SendEndpoint
{
    [MapPost("/Blockchain/Send", Tags = ["Blockchain"],
        Summary = "Send Bitcoin transactions",
        Description = "Execute bulk send operation to multiple Bitcoin addresses",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<BlockchainResponse>> Handle(
        List<BtcTransactionBO> transactionList,
        IBlockchainService blockchainService,
        CancellationToken ct)
    {
        var response = await blockchainService.BulkSendAsync(transactionList, ct);

        return response.IsSuccess
            ? Result<BlockchainResponse>.Success(response)
            : Result<BlockchainResponse>.Failure(response.Message, (int)response.HttpStatusCode);
    }
}
