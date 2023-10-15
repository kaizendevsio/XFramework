namespace XFramework.Domain.Generic.Contracts.Requests;

public record TransactionRequestBase : RequestBase
{
    public string? ClientReference { get; set; }
    public string? Description { get; set; }
    public string? Currency { get; set; }
}