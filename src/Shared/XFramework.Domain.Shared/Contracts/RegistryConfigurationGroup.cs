namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class RegistryConfigurationGroup : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public string? Description { get; set; }


    [MemoryPackOrder(2)]
    public virtual ICollection<RegistryConfiguration> RegistryConfigurations { get; set; } =
        new List<RegistryConfiguration>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
