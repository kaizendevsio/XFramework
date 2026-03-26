namespace Wallets.Api.Events;

/// <summary>
/// Publishes wallet domain events for notification and auditing.
/// </summary>
public interface IWalletEventPublisher
{
    /// <summary>
    /// Publishes a wallet event to the in-memory queue and logs it.
    /// </summary>
    Task PublishAsync(WalletEvent walletEvent);

    /// <summary>
    /// Retrieves recent events from the bounded in-memory buffer.
    /// </summary>
    /// <param name="walletId">Optional filter by wallet ID</param>
    /// <param name="credentialId">Optional filter by credential ID</param>
    /// <param name="eventType">Optional filter by event type name</param>
    /// <param name="pageIndex">Zero-based page index</param>
    /// <param name="pageSize">Number of events per page</param>
    IReadOnlyList<WalletEvent> GetRecentEvents(
        Guid? walletId = null,
        Guid? credentialId = null,
        string? eventType = null,
        int pageIndex = 0,
        int pageSize = 50);
}
