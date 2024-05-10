namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityContact : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? TypeId { get; set; }

    [MemoryPackOrder(1)]
    public string Value { get; set; } = null!;

    [MemoryPackOrder(2)]
    public Guid CredentialId { get; set; }


    [MemoryPackOrder(3)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(4)]
    public virtual IdentityContactType? Type { get; set; }

    [MemoryPackOrder(5)]
    public virtual IdentityContactGroup Group { get; set; } = null!;

    [MemoryPackOrder(6)]
    public virtual IdentityCredential Credential { get; set; } = null!;
}
