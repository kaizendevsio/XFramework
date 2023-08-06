namespace XFramework.Domain.Generic.Contracts;

public partial class IncomeTransaction
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public Guid? IdentityCredentialId { get; set; }

    public Guid? IncomeTypeId { get; set; }

    public decimal? IncomeValue { get; set; }

    public short? TransactionType { get; set; }

    public long? SourceMapId { get; set; }

    public short? IncomeStatus { get; set; }

    public string? Remarks { get; set; }

    public long? TargetMapId { get; set; }

    public long? PairMapId { get; set; }

    
    public virtual ICollection<BillsPaymentTransaction> BillsPaymentTransactions { get; } = new List<BillsPaymentTransaction>();

    public virtual IdentityCredential? IdentityCredential { get; set; }

    public virtual IncomeType? IncomeType { get; set; }

    public virtual BinaryMap? PairMap { get; set; }

    public virtual BinaryMap? SourceMap { get; set; }

    public virtual BinaryMap? TargetMap { get; set; }
}
