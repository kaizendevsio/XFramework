namespace XFramework.Domain.Generic.Contracts.Requests;

public record TransactionRequestBase : RequestBase
{
    public Guid CredentialId { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Remarks { get; set; }
    public bool OnHold { get; set; }
    public string? ReferenceNumber { get; set; }
    public required Guid CurrencyId { get; set; }
}