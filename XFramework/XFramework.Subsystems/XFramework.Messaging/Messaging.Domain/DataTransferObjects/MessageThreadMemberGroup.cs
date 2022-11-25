using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects;

public partial class MessageThreadMemberGroup
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

    public virtual MessageThread MessageThread { get; set; } = null!;

    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; } = new List<MessageThreadMember>();
}
