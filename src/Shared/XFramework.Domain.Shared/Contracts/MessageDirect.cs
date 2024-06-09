using XFramework.Domain.Shared.Enums;

namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageDirect : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? ParentMessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? TypeId { get; set; }

    [MemoryPackOrder(2)]
    public Guid? SenderId { get; set; }

    [MemoryPackOrder(3)]
    public MessageTransportType MessageTransportType { get; set; }
    
    [MemoryPackOrder(4)]
    public Guid? RecipientId { get; set; }
    
    [MemoryPackOrder(5)]
    public string? ExternalRecipient { get; set; }
    
    [MemoryPackOrder(6)]
    public string? ExternalSender { get; set; }
    
    [MemoryPackOrder(7)]
    public string? Intent { get; set; } = null!;

    [MemoryPackOrder(8)]
    public string? Subject { get; set; } = null!;

    [MemoryPackOrder(9)]
    public string Message { get; set; } = null!;
    
    [MemoryPackOrder(10)]
    public MessageStatus Status { get; set; }
    
    [MemoryPackOrder(11)]
    public Guid AgentClusterId { get; set; }
    
    [MemoryPackOrder(12)]
    public string? SubscriptionId { get; set; }
    
    [MemoryPackOrder(13)]
    public DateTime? SentAt { get; set; }
    
    [MemoryPackOrder(14)]
    public DateTime? RecievedAt { get; set; }

    [MemoryPackOrder(15)]
    public virtual ICollection<MessageDirect> InverseParentMessage { get; set; } = [];

    [MemoryPackOrder(16)]
    public virtual MessageDirect? ParentMessage { get; set; }

    [MemoryPackOrder(17)]
    public virtual IdentityCredential? Recipient { get; set; }

    [MemoryPackOrder(18)]
    public virtual IdentityCredential? Sender { get; set; } = null!;

    [MemoryPackOrder(19)]
    public virtual MessageType Type { get; set; } = null!;
}
