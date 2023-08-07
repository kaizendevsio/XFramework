namespace XFramework.Domain.Generic.Contracts;

public partial class EloadTransaction
{
    public Guid Id { get; set; }

    public Guid? EloadProductCodeId { get; set; }

    public decimal? Discount { get; set; }

    public Guid CredentialId { get; set; }

    public string? SenderNumber { get; set; }

    public string? CustomerNumber { get; set; }

    public decimal? PreviousBalance { get; set; }

    public decimal? CurrentBalance { get; set; }

    public string? TransactionId { get; set; }

    public Guid? WalletTypeId { get; set; }

    public string? RawRequest { get; set; }

    public string? RawResponse { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    public int? Status { get; set; }

    public string? Amount { get; set; }


    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual EloadProductCode? EloadProductCode { get; set; }
}
