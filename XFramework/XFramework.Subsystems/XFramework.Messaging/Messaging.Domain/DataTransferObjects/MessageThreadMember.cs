using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects;

public partial class MessageThreadMember
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public short Status { get; set; }

    public string Emoji { get; set; } = null!;

    public string Alias { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Guid { get; set; } = null!;

    public long MessageThreadId { get; set; }

    public long IdentityCredentialId { get; set; }

    public long GroupId { get; set; }

    public virtual MessageThreadMemberGroup Group { get; set; } = null!;

    public virtual IdentityCredential IdentityCredential { get; set; } = null!;

    public virtual ICollection<MessageDelivery> MessageDeliveries { get; } = new List<MessageDelivery>();

    public virtual MessageThread MessageThread { get; set; } = null!;

    public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; } = new List<MessageThreadMemberRole>();

    public virtual ICollection<Message> Messages { get; } = new List<Message>();
}
