using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Coins.Core.Interfaces.Wrappers;
using Coins.Domain.BusinessObjects;

namespace Coins.Core.Services
{
    /// <summary>
    /// Service for managing blockchain operations (Bitcoin)
    /// Consolidated from BulkSendHandler
    /// </summary>
    public class BlockchainService : IBlockchainService
    {
        private readonly IBtcBlockchainWrapper _btcBlockchainWrapper;

        public BlockchainService(IBtcBlockchainWrapper btcBlockchainWrapper)
        {
            _btcBlockchainWrapper = btcBlockchainWrapper ?? throw new ArgumentNullException(nameof(btcBlockchainWrapper));
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
                    return new BlockchainResponse
                    {
                        HttpStatusCode = HttpStatusCode.BadRequest,
                        Message = "Transaction list cannot be null or empty"
                    };
                }

                // Execute bulk send operation via blockchain wrapper
                var response = await _btcBlockchainWrapper.SendToMany(transactionList);

                // Build and return response object
                return new BlockchainResponse
                {
                    HttpStatusCode = response.StatusCode,
                    Message = response.ReasonPhrase
                };
            }
            catch (Exception ex)
            {
                // Build and return error response
                return new BlockchainResponse
                {
                    HttpStatusCode = HttpStatusCode.BadRequest,
                    Message = $"{ex.Message} : {ex.InnerException?.Message}"
                };
            }
        }
    }
}