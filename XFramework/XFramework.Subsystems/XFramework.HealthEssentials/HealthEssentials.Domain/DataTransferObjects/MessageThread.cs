using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects;

public partial class MessageThread
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Guid { get; set; } = null!;

    public long EntityId { get; set; }

    public virtual MessageThreadEntity Entity { get; set; } = null!;

    public virtual ICollection<MessageThreadMemberGroup> MessageThreadMemberGroups { get; } = new List<MessageThreadMemberGroup>();

    public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; } = new List<MessageThreadMember>();

    public virtual ICollection<Message> Messages { get; } = new List<Message>();
}
