namespace XFramework.Domain.Generic.Contracts;

public partial class MessageThread : BaseModel
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;


    public Guid TypeId { get; set; }

    public virtual MessageThreadType Type { get; set; } = null!;

    public virtual ICollection<MessageThreadMemberGroup> MessageThreadMemberGroups { get; } =
        new List<MessageThreadMemberGroup>();

    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; } = new List<MessageThreadMember>();

    public virtual ICollection<Message> Messages { get; } = new List<Message>();
}