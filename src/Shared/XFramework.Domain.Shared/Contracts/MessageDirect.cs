using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Domain.Shared.Contracts;

public partial class MessageDirect : BaseModel
{
    public Guid? ParentMessageId { get; set; }

    public Guid? TypeId { get; set; }

    public Guid? SenderId { get; set; }

    public MessageTransportType MessageTransportType { get; set; }
    
    public Guid? RecipientId { get; set; }
    
    public string? ExternalRecipient { get; set; }
    
    public string Intent { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Message { get; set; } = null!;


    public MessageStatus Status { get; set; }

    public virtual ICollection<MessageDirect> InverseParentMessage { get; set; } = [];

    public virtual MessageDirect? ParentMessage { get; set; }

    public virtual IdentityCredential? Recipient { get; set; }

    public virtual IdentityCredential? Sender { get; set; } = null!;

    public virtual MessageType Type { get; set; } = null!;
}