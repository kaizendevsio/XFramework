using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects
{
    public partial class MessageReaction
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long MessageId { get; set; }
        public long EntityId { get; set; }

        public virtual MessageReactionEntity Entity { get; set; } = null!;
        public virtual Message Message { get; set; } = null!;
    }
}
