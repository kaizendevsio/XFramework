namespace XFramework.Domain.Generic.Contracts;

public partial class BusinessPackage
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public Guid? IdentityCredentialId { get; set; }

    public short? PackageStatus { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public Guid? UserDepositRequestId { get; set; }

    public DateTime? CancellationDate { get; set; }

    public DateTime? ActivationDate { get; set; }

    public Guid? RecipientIdentityCredentialId { get; set; }

    public string? CodeString { get; set; }

    public long? ConsumedBy { get; set; }

    public int? PackageType { get; set; }

    public string? CodeHash { get; set; }

    public string? Remarks { get; set; }

    
    public virtual BinaryMap? BinaryMap { get; set; }

    public virtual ICollection<BusinessPackageUpgradeTransaction> BusinessPackageUpgradeTransactions { get; } = new List<BusinessPackageUpgradeTransaction>();
    public virtual ICollection<IncomeDistribution> IncomeDistributions { get; } = new List<IncomeDistribution>();

    public virtual ICollection<CommissionDeductionRequest> CommissionDeductionRequests { get; } = new List<CommissionDeductionRequest>();

    public virtual IdentityCredential? ConsumedByNavigation { get; set; }

    public virtual IdentityCredential? IdentityCredential { get; set; }

    public virtual IdentityCredential? RecipientIdentityCredential { get; set; }

    public virtual DepositRequest? UserDepositRequest { get; set; }
}
