namespace XFramework.Domain.Generic.Contracts;

public partial class BillsPaymentTransaction : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid? BillerId { get; set; }

    public string? RawRequest { get; set; }

    public string? RawResponse { get; set; }

    public int? Status { get; set; }

    public decimal? Amount { get; set; }

    public string? ReferenceNo { get; set; }

    public decimal? Discount { get; set; }

    public decimal? ServiceCharge { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? ResponseReasonPhrase { get; set; }

    public int? ResponseHttpStatus { get; set; }

    public decimal? ConvenienceFee { get; set; }

    public Guid? IncomeTransactionId { get; set; }

    public virtual Biller? Biller { get; set; }

    public virtual IdentityCredential Credential { get; set; }

    public virtual IncomeTransaction? IncomeTransaction { get; set; }
}