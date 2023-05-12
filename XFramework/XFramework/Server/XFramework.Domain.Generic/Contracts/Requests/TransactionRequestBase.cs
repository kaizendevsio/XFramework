namespace XFramework.Domain.Generic.Contracts.Requests;

public class TransactionRequestBase : RequestBase
{
    public string? ClientReference { get; set; }
    public string? Description { get; set; }
    public string? Currency { get; set; }
}