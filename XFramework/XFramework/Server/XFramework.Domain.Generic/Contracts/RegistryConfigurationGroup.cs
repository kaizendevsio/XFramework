namespace XFramework.Domain.Generic.Contracts;

public partial record RegistryConfigurationGroup : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; } =
        new List<RegistryConfiguration>();
}