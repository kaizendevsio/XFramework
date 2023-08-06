namespace XFramework.Domain.Generic.Contracts;

public partial class BusinessPackageUpgradeTransaction
{
    public Guid Id { get; set; }

    public bool IsDeleted { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public Guid IdentityCredentialId { get; set; }

    public long UserBusinessPackageId { get; set; }
    
    public long? CurrentBusinessPackageId { get; set; }

    public long PreviousBusinessPackageId { get; set; }

    public int? Status { get; set; }

    public virtual IdentityCredential IdentityCredential { get; set; } = null!;

    public virtual BusinessPackage CurrentBusinessPackage { get; set; } = null!;
    
    public virtual BusinessPackage PreviousBusinessPackage { get; set; } = null!;
}
