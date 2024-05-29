namespace XFramework.Domain.Shared.Contracts.Requests;

public record TransactionRequestBase : RequestBase
{
    public Guid CredentialId { get; set; }
    public virtual decimal TotalAmount => LineItems.Sum(x => x.Amount ?? 0);
    public virtual decimal Amount { get; set; }
    public virtual decimal Fee { get; set; }
    public virtual decimal TotalFee => LineItems.Sum(x => x.Fee) + Fee;
    public string? Remarks { get; set; }
    public bool OnHold { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid CurrencyId { get; set; }
    
    public List<WalletTransactionLineItem> LineItems { get; set; } = [];
}