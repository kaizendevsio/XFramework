using Coins.Api.BusinessObjects;
using Coins.Api.Interfaces.Wrappers;

namespace Coins.Api.Services;

/// <summary>
/// Service for managing blockchain operations (Bitcoin)
/// Consolidated from BulkSendHandler
/// </summary>
public class BlockchainService : IBlockchainService
{
    private readonly IBtcBlockchainWrapper _btcBlockchainWrapper;
    private readonly ILogger<BlockchainService> _logger;

    public BlockchainService(
        IBtcBlockchainWrapper btcBlockchainWrapper,
        ILogger<BlockchainService> logger)
    {
        _btcBlockchainWrapper = btcBlockchainWrapper ?? throw new ArgumentNullException(nameof(btcBlockchainWrapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute a bulk send operation to multiple Bitcoin addresses
    /// </summary>
    /// <param name="transactionList">List of Bitcoin transactions to send</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Response with status code and message</returns>
    public async Task<BlockchainResponse> BulkSendAsync(
        List<BtcTransactionBO> transactionList,
        CancellationToken ct = default)
    {
        try
        {
            // Validate input
            if (transactionList == null || transactionList.Count == 0)
            {
                _logger.LogWarning("BulkSend validation failed: Transaction list cannot be null or empty");
                return new BlockchainResponse
                {
                    HttpStatusCode = HttpStatusCode.BadRequest,
                    Message = "Transaction list cannot be null or empty"
                };
            }

            _logger.LogInformation("Initiating bulk send operation for {Count} transactions", transactionList.Count);

            // Execute bulk send operation via blockchain wrapper
            var response = await _btcBlockchainWrapper.SendToMany(transactionList);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Bulk send operation completed successfully for {Count} transactions", transactionList.Count);
            }
            else
            {
                _logger.LogWarning("Bulk send operation returned status {StatusCode}: {Reason}",
                    response.StatusCode, response.ReasonPhrase);
            }

            // Build and return response object
            return new BlockchainResponse
            {
                HttpStatusCode = response.StatusCode,
                Message = response.ReasonPhrase ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing bulk send operation for {Count} transactions", transactionList?.Count ?? 0);
            
            // Build and return error response
            return new BlockchainResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"{ex.Message} : {ex.InnerException?.Message}"
            };
        }
    }
}