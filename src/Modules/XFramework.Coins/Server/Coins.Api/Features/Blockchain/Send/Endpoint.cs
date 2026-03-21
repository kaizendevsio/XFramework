using Coins.Api.BusinessObjects;
using Coins.Api.Services;

namespace Coins.Api.Features.Blockchain.Send;

/// <summary>
/// POST /Blockchain/Send endpoint - Bulk send Bitcoin transactions
/// </summary>
public static class SendEndpoint
{
    public static async Task<IResult> HandleAsync(
        List<BtcTransactionBO> transactionList,
        IBlockchainService blockchainService,
        IValidator<List<BtcTransactionBO>> validator,
        CancellationToken cancellationToken = default)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(transactionList, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return Results.BadRequest(new { Errors = errors });
        }

        // Execute bulk send
        var response = await blockchainService.BulkSendAsync(transactionList, cancellationToken);

        return response.HttpStatusCode switch
        {
            HttpStatusCode.OK => Results.Ok(response),
            HttpStatusCode.Accepted => Results.Accepted(null, response),
            HttpStatusCode.BadRequest => Results.BadRequest(response),
            _ => Results.StatusCode((int)response.HttpStatusCode)
        };
    }
}