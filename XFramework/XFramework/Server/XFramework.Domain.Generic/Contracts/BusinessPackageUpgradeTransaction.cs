namespace XFramework.Domain.Generic.Contracts;

public partial class BusinessPackageUpgradeTransaction
{
    public Guid Id { get; set; }

    public bool IsDeleted { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public Guid CredentialId { get; set; }

    public Guid UserBusinessPackageId { get; set; }
    
    public Guid CurrentBusinessPackageId { get; set; }

    public Guid PreviousBusinessPackageId { get; set; }

    public int? Status { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual BusinessPackage CurrentBusinessPackage { get; set; } = null!;
    
}
