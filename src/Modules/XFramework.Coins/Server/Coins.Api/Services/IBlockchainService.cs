using System.Net;
using Coins.Api.BusinessObjects;

namespace Coins.Api.Services;

/// <summary>
/// Service for managing blockchain operations (Bitcoin)
/// </summary>
public interface IBlockchainService
{
    /// <summary>
    /// Execute a bulk send operation to multiple Bitcoin addresses
    /// </summary>
    /// <param name="transactionList">List of Bitcoin transactions to send</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Response with status code and message</returns>
    Task<BlockchainResponse> BulkSendAsync(
        List<BtcTransactionBO> transactionList,
        CancellationToken ct = default);
}

/// <summary>
/// Response from blockchain operations
/// </summary>
public class BlockchainResponse
{
    public HttpStatusCode HttpStatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess => HttpStatusCode == HttpStatusCode.OK || HttpStatusCode == HttpStatusCode.Accepted;
}