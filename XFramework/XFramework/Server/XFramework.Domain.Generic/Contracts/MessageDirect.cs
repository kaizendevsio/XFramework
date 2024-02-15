namespace XFramework.Domain.Generic.Contracts;

public partial class MessageDirect : BaseModel
{
    public Guid ParentMessageId { get; set; }

    public Guid TypeId { get; set; }

    public Guid SenderId { get; set; }

    public Guid RecipientId { get; set; }

    public string Sender { get; set; } = null!;

    public string Recipient { get; set; } = null!;

    public string Intent { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Message { get; set; } = null!;


    public short Status { get; set; }

    public virtual ICollection<MessageDirect> InverseParentMessage { get; set; } = new List<MessageDirect>();

    public virtual MessageDirect? ParentMessage { get; set; }

    public virtual IdentityCredential? RecipientNavigation { get; set; }

    public virtual IdentityCredential SenderNavigation { get; set; } = null!;

    public virtual MessageType Type { get; set; } = null!;
}