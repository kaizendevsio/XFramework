using System;
using System.Collections.Generic;

namespace SmsGateway.Domain.DataTransferObjects
{
    public partial class Message
    {
        public Message()
        {
            MessageDeliveries = new HashSet<MessageDelivery>();
            MessageFiles = new HashSet<MessageFile>();
            MessageReactions = new HashSet<MessageReaction>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Text { get; set; } = null!;
        public string Guid { get; set; } = null!;
        public long MessageThreadId { get; set; }
        public long MessageThreadMemberId { get; set; }

        public virtual MessageThread MessageThread { get; set; } = null!;
        public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
        public virtual ICollection<MessageDelivery> MessageDeliveries { get; set; }
        public virtual ICollection<MessageFile> MessageFiles { get; set; }
        public virtual ICollection<MessageReaction> MessageReactions { get; set; }
    }
}
