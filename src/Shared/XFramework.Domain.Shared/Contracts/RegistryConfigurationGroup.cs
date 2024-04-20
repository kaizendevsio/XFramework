using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class RegistryConfigurationGroup : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; set; } =
        new List<RegistryConfiguration>();
}