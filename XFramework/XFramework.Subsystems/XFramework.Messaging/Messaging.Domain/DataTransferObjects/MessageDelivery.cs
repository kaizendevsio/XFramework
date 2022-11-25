using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects;

public partial class MessageDelivery
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Guid { get; set; } = null!;

    public long MessageThreadMemberId { get; set; }

    public long MessageId { get; set; }

    public long EntityId { get; set; }

    public virtual MessageDeliveryEntity Entity { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;

    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
}
