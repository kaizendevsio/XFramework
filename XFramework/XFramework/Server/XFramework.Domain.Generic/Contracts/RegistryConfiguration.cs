namespace XFramework.Domain.Generic.Contracts;

public partial class RegistryConfiguration : BaseModel
{
    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public Guid GroupId { get; set; }

    public string? Unit { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual RegistryConfigurationGroup? Group { get; set; }
}