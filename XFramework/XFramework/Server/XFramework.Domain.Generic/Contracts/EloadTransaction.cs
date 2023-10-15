namespace XFramework.Domain.Generic.Contracts;

public partial record EloadTransaction : BaseModel
{
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


    public int? Status { get; set; }

    public string? Amount { get; set; }


    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual EloadProductCode? EloadProductCode { get; set; }
}