namespace XFramework.Domain.Generic.Contracts;

public partial class BusinessPackage : BaseModel
{
    public Guid CredentialId { get; set; }

    public short? PackageStatus { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public Guid? UserDepositRequestId { get; set; }

    public DateTime? CancellationDate { get; set; }

    public DateTime? ActivationDate { get; set; }

    public Guid? RecipientCredentialId { get; set; }

    public string? CodeString { get; set; }

    public Guid? ConsumedById { get; set; }

    public Guid TypeId { get; set; }

    public string? CodeHash { get; set; }

    public string? Remarks { get; set; }

    
    public virtual ICollection<BusinessPackageUpgradeTransaction> BusinessPackageUpgradeTransactions { get; set; } =
        new List<BusinessPackageUpgradeTransaction>();

    public virtual ICollection<BusinessPackageInclusion> BusinessPackageInclusions { get; set; } =
        new List<BusinessPackageInclusion>();


    public virtual ICollection<CommissionDeductionRequest> CommissionDeductionRequests { get; set; } =
        new List<CommissionDeductionRequest>();

    public virtual IdentityCredential? ConsumedBy { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;
    public virtual BusinessPackageType Type { get; set; } = null!;

    public virtual IdentityCredential? RecipientIdentityCredential { get; set; }

    public virtual DepositRequest? UserDepositRequest { get; set; }
}