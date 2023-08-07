namespace XFramework.Domain.Generic.Contracts;

public partial class Application
{
    public Guid Id { get; set; }

    public string? AppName { get; set; }

    public string? Description { get; set; }

    
    public short? Status { get; set; }

    public DateTime? Expiration { get; set; }

    public DateTime? AvailabilityDate { get; set; }

    public Guid? ParentAppId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Guid EnterpriseId { get; set; }

    public decimal Version { get; set; }

    public virtual Enterprise Enterprise { get; set; } = null!;

    public virtual ICollection<IdentityCredential> IdentityCredentials { get; } = new List<IdentityCredential>();

    public virtual ICollection<IdentityInformation> IdentityInformations { get; } = new List<IdentityInformation>();

    public virtual ICollection<IdentityRoleType> IdentityRoleTypes { get; } = new List<IdentityRoleType>();

    public virtual ICollection<Log> Logs { get; } = new List<Log>();

    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; } = new List<RegistryConfiguration>();

    public virtual ICollection<WalletType> WalletTypes { get; } = new List<WalletType>();
}
