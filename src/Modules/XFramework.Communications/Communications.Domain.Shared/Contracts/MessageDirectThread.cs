namespace Communications.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageDirectThread : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageThreadId { get; set; }

    [MemoryPackOrder(1)]
    public Guid FirstCredentialId { get; set; }

    [MemoryPackOrder(2)]
    public Guid SecondCredentialId { get; set; }

    [MemoryPackOrder(3)]
    public virtual MessageThread MessageThread { get; set; } = null!;
}
