using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class MessageThreadMember : BaseModel
{
    public short Status { get; set; }

    public string Emoji { get; set; } = null!;

    public string Alias { get; set; } = null!;

    public string Description { get; set; } = null!;


    public Guid MessageThreadId { get; set; }

    public Guid CredentialId { get; set; }

    public Guid GroupId { get; set; }

    public virtual MessageThreadMemberGroup Group { get; set; } = null!;

    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual ICollection<MessageDelivery> MessageDeliveries { get; set; } = new List<MessageDelivery>();

    public virtual MessageThread MessageThread { get; set; } = null!;

    public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; set; } =
        new List<MessageThreadMemberRole>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}