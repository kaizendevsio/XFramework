namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class Session : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? SessionTypeId { get; set; }

    [MemoryPackOrder(1)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(2)]
    public string? SessionData { get; set; }

    [MemoryPackOrder(3)]
    public virtual SessionType? SessionType { get; set; }

    [MemoryPackOrder(4)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}
