namespace XFramework.Domain.Generic.Contracts;

public partial class MessageType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public short Priority { get; set; }

    
    public virtual ICollection<MessageDirect> MessageDirects { get; } = new List<MessageDirect>();

    public virtual ICollection<MessageThreadType> MessageThreadTypes { get; } = new List<MessageThreadType>();
}
