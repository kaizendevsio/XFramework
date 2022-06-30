using System;
using System.Collections.Generic;

namespace SmsGateway.Domain.DataTransferObjects
{
    public partial class MessageThread
    {
        public MessageThread()
        {
            MessageThreadMemberGroups = new HashSet<MessageThreadMemberGroup>();
            MessageThreadMembers = new HashSet<MessageThreadMember>();
            Messages = new HashSet<Message>();
        }

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
        public virtual ICollection<MessageThreadMemberGroup> MessageThreadMemberGroups { get; set; }
        public virtual ICollection<MessageThreadMember> MessageThreadMembers { get; set; }
        public virtual ICollection<Message> Messages { get; set; }
    }
}
