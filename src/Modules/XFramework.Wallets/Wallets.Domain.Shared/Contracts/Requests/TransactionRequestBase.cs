namespace Wallets.Domain.Shared.Contracts.Requests;

public record TransactionRequestBase : RequestBase
{
    public Guid CredentialId { get; set; }
    public virtual decimal TotalAmount => LineItems.Sum(x => x.Amount ?? 0) + Amount;
    public virtual decimal Amount { get; set; }
    public virtual decimal Fee { get; set; }
    public virtual decimal TotalFee => LineItems.Sum(x => x.Fee) + Fee;
    public string? Remarks { get; set; }
    public bool OnHold { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid CurrencyId { get; set; }

    /// <summary>
    /// Optional idempotency key to prevent duplicate financial transactions.
    /// When provided, the system checks if a transaction with this key already exists
    /// and returns the existing result instead of creating a duplicate.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public List<WalletTransactionLineItem> LineItems { get; set; } = [];
}