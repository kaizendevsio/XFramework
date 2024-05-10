namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class RegistryConfiguration : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Key { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string? Value { get; set; }

    [MemoryPackOrder(2)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(3)]
    public string? Unit { get; set; }

    [MemoryPackOrder(4)]
    public virtual Tenant Tenant { get; set; } = null!;

    [MemoryPackOrder(5)]
    public virtual RegistryConfigurationGroup? Group { get; set; }
}
