namespace XFramework.Domain.Generic.Contracts;

public partial record MessageThreadType : BaseModel
{
    public string Name { get; set; } = null!;


    public Guid MessageTypeId { get; set; }

    public virtual ICollection<MessageThread> MessageThreads { get; } = new List<MessageThread>();

    public virtual MessageType MessageType { get; set; } = null!;
}