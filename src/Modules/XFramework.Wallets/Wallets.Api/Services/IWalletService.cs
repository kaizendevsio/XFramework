using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;

namespace Wallets.Api.Services;

/// <summary>
/// Service for managing wallet operations including balance changes, transfers, and conversions.
/// Provides direct service-based access to wallet functionality without MediatR indirection.
/// </summary>
public interface IWalletService
{
    /// <summary>
    /// Creates a new wallet for a credential with the specified wallet type.
    /// Automatically generates a unique account number and applies wallet type rules.
    /// </summary>
    /// <param name="credentialId">The credential ID that will own the wallet</param>
    /// <param name="walletTypeId">The type of wallet to create</param>
    /// <param name="initialBalance">Optional initial balance (default: 0)</param>
    /// <param name="tenantId">The tenant ID for multi-tenancy support</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the created wallet</returns>
    Task<Result<Wallet>> CreateWalletAsync(
        Guid credentialId,
        Guid walletTypeId,
        decimal initialBalance,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a wallet by its ID.
    /// </summary>
    /// <param name="walletId">The wallet ID to retrieve</param>
    /// <param name="tenantId">The tenant ID for multi-tenancy support</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the wallet if found</returns>
    Task<Result<Wallet>> GetWalletAsync(
        Guid walletId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all wallets for a specific credential.
    /// </summary>
    /// <param name="credentialId">The credential ID to retrieve wallets for</param>
    /// <param name="tenantId">The tenant ID for multi-tenancy support</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the list of wallets</returns>
    Task<Result<List<Wallet>>> GetWalletsByCredentialAsync(
        Guid credentialId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments (adds to) a wallet's balance.
    /// Supports both immediate and on-hold increments.
    /// Automatically creates wallet if WalletTypeId is provided and wallet doesn't exist.
    /// </summary>
    /// <param name="request">The increment request containing wallet details and amount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// Validates:
    /// - Amount > 0
    /// - Min/Max transfer rules
    /// - Maintaining balance rule
    /// Creates WalletTransaction record with TransactionType.Credit
    /// </remarks>
    Task<Result> IncrementBalanceAsync(
        IncrementWalletRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrements (subtracts from) a wallet's balance.
    /// Supports both immediate and on-hold decrements.
    /// </summary>
    /// <param name="request">The decrement request containing wallet details and amount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// Validates:
    /// - Amount > 0
    /// - Sufficient available balance
    /// - Maintaining balance rule
    /// Creates WalletTransaction record with TransactionType.Debit
    /// </remarks>
    Task<Result> DecrementBalanceAsync(
        DecrementWalletRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers funds from one wallet to another.
    /// Handles fee deduction based on TransferDeductionType (DeductFromSender or DeductFromRecipient).
    /// Automatically creates recipient wallet if it doesn't exist.
    /// </summary>
    /// <param name="request">The transfer request containing sender, recipient, and transfer details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// Validates:
    /// - Amount > 0, Fee >= 0
    /// - Both wallets exist (creates recipient if needed)
    /// - Sender has sufficient balance
    /// - Min/Max transfer rules for both wallets
    /// - Bond balance rule
    /// - Maintaining balance rule
    /// Creates:
    /// - Two WalletTransaction records (debit for sender, credit for recipient)
    /// - One WalletTransfer entity linking the transactions
    /// </remarks>
    Task<Result> TransferAsync(
        TransferWalletRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts funds from one wallet type to another for the same credential.
    /// Handles fee deduction based on TransferDeductionType.
    /// Automatically creates target wallet if it doesn't exist.
    /// </summary>
    /// <param name="request">The conversion request containing source/target wallet types and amount</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// Validates:
    /// - Amount > 0, Fee >= 0
    /// - Source wallet exists
    /// - Target wallet exists (creates if needed)
    /// - Sufficient balance in source wallet
    /// Creates:
    /// - Two WalletTransaction records (debit from source, credit to target)
    /// </remarks>
    Task<Result> ConvertWalletAsync(
        ConvertWalletRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a transaction that was previously placed on hold.
    /// Moves the amount from on-hold balances to available balances.
    /// </summary>
    /// <param name="request">The release request containing the transaction ID to release</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    /// <remarks>
    /// Validates:
    /// - Transaction exists
    /// - Transaction is currently held (Held = true)
    /// Updates:
    /// - Transaction.Held = false, Transaction.Released = true
    /// - Wallet balances based on transaction type (Credit or Debit)
    /// </remarks>
    Task<Result> ReleaseTransactionAsync(
        ReleaseTransactionRequest request,
        CancellationToken cancellationToken = default);
}