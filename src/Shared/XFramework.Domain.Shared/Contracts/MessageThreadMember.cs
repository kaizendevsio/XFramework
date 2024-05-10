namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageThreadMember : BaseModel
{
    
    [MemoryPackOrder(0)]
    public short Status { get; set; }

    [MemoryPackOrder(1)]
    public string Emoji { get; set; } = null!;

    [MemoryPackOrder(2)]
    public string Alias { get; set; } = null!;

    [MemoryPackOrder(3)]
    public string Description { get; set; } = null!;


    [MemoryPackOrder(4)]
    public Guid MessageThreadId { get; set; }

    [MemoryPackOrder(5)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(6)]
    public Guid GroupId { get; set; }

    [MemoryPackOrder(7)]
    public virtual MessageThreadMemberGroup Group { get; set; } = null!;

    [MemoryPackOrder(8)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(9)]
    public virtual ICollection<MessageDelivery> MessageDeliveries { get; set; } = new List<MessageDelivery>();

    [MemoryPackOrder(10)]
    public virtual MessageThread MessageThread { get; set; } = null!;

    [MemoryPackOrder(11)]
    public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; set; } =
        new List<MessageThreadMemberRole>();

    [MemoryPackOrder(12)]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
