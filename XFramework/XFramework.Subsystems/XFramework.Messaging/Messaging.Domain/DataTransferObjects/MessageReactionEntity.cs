using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects
{
    public partial class MessageReactionEntity
    {
        public MessageReactionEntity()
        {
            MessageReactions = new HashSet<MessageReaction>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = null!;
        public string Emoji { get; set; } = null!;

        public virtual ICollection<MessageReaction> MessageReactions { get; set; }
    }
}
