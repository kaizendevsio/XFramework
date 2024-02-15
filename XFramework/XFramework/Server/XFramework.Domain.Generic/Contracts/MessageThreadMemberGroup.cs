namespace XFramework.Domain.Generic.Contracts;

public partial class MessageThreadMemberGroup : BaseModel
{
    public short Status { get; set; }

    public string Emoji { get; set; } = null!;

    public string Alias { get; set; } = null!;

    public string Description { get; set; } = null!;


    public Guid MessageThreadId { get; set; }

    public virtual MessageThread MessageThread { get; set; } = null!;

    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; set; } = new List<MessageThreadMember>();
}