namespace XFramework.Domain.Generic.Contracts;

public partial class MessageDirect
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long? ParentMessageId { get; set; }

    public Guid TypeId { get; set; }

    public long SenderId { get; set; }

    public long? RecipientId { get; set; }

    public string Sender { get; set; } = null!;

    public string Recipient { get; set; } = null!;

    public string Intent { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Message { get; set; } = null!;

    
    public short Status { get; set; }

    public virtual ICollection<MessageDirect> InverseParentMessage { get; } = new List<MessageDirect>();

    public virtual MessageDirect? ParentMessage { get; set; }

    public virtual IdentityCredential? RecipientNavigation { get; set; }

    public virtual IdentityCredential SenderNavigation { get; set; } = null!;

    public virtual MessageType Type { get; set; } = null!;
}
