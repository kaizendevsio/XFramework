namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityVerificationType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public long? DefaultExpiry { get; set; }

    [MemoryPackOrder(2)]
    public short? Priority { get; set; }


    [MemoryPackOrder(3)]
    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();
}
