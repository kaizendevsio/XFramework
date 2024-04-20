using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class MessageThreadType : BaseModel
{
    public string Name { get; set; } = null!;


    public Guid MessageTypeId { get; set; }

    public virtual ICollection<MessageThread> MessageThreads { get; set; } = new List<MessageThread>();

    public virtual MessageType MessageType { get; set; } = null!;
}