using Coins.Api.BusinessObjects;

namespace Coins.Api.Interfaces.Wrappers;

/// <summary>
/// Wrapper interface for Bitcoin blockchain operations
/// </summary>
public interface IBtcBlockchainWrapper
{
    /// <summary>
    /// Satoshi to Bitcoin conversion factor
    /// </summary>
    decimal Satoshi { get; }
    
    /// <summary>
    /// Get the gap limit for HD wallet
    /// </summary>
    int GetGapLimit();
    
    /// <summary>
    /// Get current Bitcoin fee recommendations
    /// </summary>
    Task<BitcoinfeeBO> GetBitcoinFee();
    
    /// <summary>
    /// Send Bitcoin to multiple addresses
    /// </summary>
    /// <param name="transactionList">List of transactions</param>
    /// <returns>HTTP response with transaction result</returns>
    Task<HttpResponseMessage> SendToMany(List<BtcTransactionBO> transactionList);
    
    /// <summary>
    /// Enable HD wallet functionality
    /// </summary>
    Task<bool> EnableHd();
}