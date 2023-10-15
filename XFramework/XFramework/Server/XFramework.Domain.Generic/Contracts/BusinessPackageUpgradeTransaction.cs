namespace XFramework.Domain.Generic.Contracts;

public partial record BusinessPackageUpgradeTransaction : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid UserBusinessPackageId { get; set; }

    public Guid CurrentBusinessPackageId { get; set; }

    public Guid PreviousBusinessPackageId { get; set; }

    public int? Status { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual BusinessPackage CurrentBusinessPackage { get; set; } = null!;
}