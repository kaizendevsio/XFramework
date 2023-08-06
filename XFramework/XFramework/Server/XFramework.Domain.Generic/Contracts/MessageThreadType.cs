namespace XFramework.Domain.Generic.Contracts;

public partial class MessageThreadType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    
    public Guid MessageTypeId { get; set; }

    public virtual ICollection<MessageThread> MessageThreads { get; } = new List<MessageThread>();

    public virtual MessageType MessageType { get; set; } = null!;
}
