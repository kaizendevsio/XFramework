namespace XFramework.Domain.Generic.Contracts;

public partial class RegistryConfigurationGroup
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public bool? IsDeleted { get; set; }

    
    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; } = new List<RegistryConfiguration>();
}
