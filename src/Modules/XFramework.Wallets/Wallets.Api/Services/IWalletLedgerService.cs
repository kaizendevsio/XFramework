using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed record WalletLedgerExecutionRequest
{
    public Guid TenantId { get; init; }
    public WalletOperationType OperationType { get; init; }
    public Guid? ActorCredentialId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? RequestHash { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? CorrelationId { get; init; }
    public string? ExternalReference { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<WalletLedgerPostingRequest> Postings { get; init; } = [];
    public IReadOnlyList<Wallet> NewWallets { get; init; } = [];
    public IReadOnlyList<object> ReadModels { get; init; } = [];
    public IReadOnlyList<WalletTransactionStateUpdateRequest> TransactionUpdates { get; init; } = [];
}

public sealed record WalletLedgerPostingRequest
{
    public Guid? WalletId { get; init; }
    public Guid? WalletTransactionId { get; init; }
    public WalletTransaction? WalletTransaction { get; init; }
    public Guid? CurrencyId { get; init; }
    public Guid? WalletTypeId { get; init; }
    public WalletLedgerDirection Direction { get; init; }
    public WalletBalanceBucket BalanceBucket { get; init; }
    public WalletLedgerEntryKind EntryKind { get; init; } = WalletLedgerEntryKind.Principal;
    public decimal Amount { get; init; }
    public string? Description { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? CounterpartyType { get; init; }
    public string? CounterpartyReference { get; init; }
}

public sealed record WalletBalanceExecutionResult(
    Guid WalletId,
    decimal Balance,
    decimal AvailableBalance,
    decimal TransferableBalance,
    decimal DebitOnHoldBalance,
    decimal CreditOnHoldBalance,
    decimal TotalBalance);

public sealed record WalletTransactionStateUpdateRequest
{
    public required WalletTransaction Transaction { get; init; }
    public required Guid WalletId { get; init; }
    public bool? Held { get; init; }
    public bool? Released { get; init; }
    public bool UpdateRunningBalances { get; init; } = true;
}

public sealed record WalletLedgerExecutionResult(
    Guid OperationId,
    bool AlreadyProcessed,
    IReadOnlyDictionary<Guid, WalletBalanceExecutionResult> Wallets);

public interface IWalletLedgerService
{
    Task<Result<WalletLedgerExecutionResult>> ExecuteAsync(
        WalletLedgerExecutionRequest request,
        CancellationToken ct = default);

    Task<bool> HasProcessedAsync(
        Guid tenantId,
        string? idempotencyKey,
        CancellationToken ct = default);
}
