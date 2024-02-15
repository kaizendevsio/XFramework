namespace XFramework.Domain.Generic.Contracts;

public partial class Tenant : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }


    public short? Status { get; set; }

    public DateTime? Expiration { get; set; }

    public DateTime? AvailabilityDate { get; set; }

    public Guid? ParentTenantId { get; set; }
    
    public decimal Version { get; set; }
    
    public virtual ICollection<IdentityCredential> IdentityCredentials { get; set; } = new List<IdentityCredential>();

    public virtual ICollection<IdentityInformation> IdentityInformations { get; set; } = new List<IdentityInformation>();

    public virtual ICollection<IdentityRoleType> IdentityRoleTypes { get; set; } = new List<IdentityRoleType>();

    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; set; } =
        new List<RegistryConfiguration>();

    public virtual ICollection<WalletType> WalletTypes { get; set; } = new List<WalletType>();
}