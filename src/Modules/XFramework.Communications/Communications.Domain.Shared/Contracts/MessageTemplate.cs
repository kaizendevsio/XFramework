namespace Communications.Domain.Shared.Contracts;

using Communications.Domain.Shared;

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageTemplate : BaseModel
{
    [MemoryPackOrder(0)]
    public string Key { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(2)]
    public string? Description { get; set; }

    [MemoryPackOrder(3)]
    public string TemplateType { get; set; } = MessageTemplateTypes.Tenant;

    [MemoryPackOrder(4)]
    public string? Subject { get; set; }

    [MemoryPackOrder(5)]
    public string Body { get; set; } = null!;

    [MemoryPackOrder(6)]
    public string RequiredVariablesJson { get; set; } = "[]";

    [MemoryPackOrder(7)]
    public Guid? OwnerCredentialId { get; set; }

    [MemoryPackOrder(8)]
    public bool IsDefault { get; set; }

    [MemoryPackOrder(9)]
    public bool IsLocked { get; set; }

    [MemoryPackOrder(10)]
    public Guid? SystemReferenceId { get; set; }

    [MemoryPackOrder(11)]
    public virtual IdentityCredential? OwnerCredential { get; set; }
}
