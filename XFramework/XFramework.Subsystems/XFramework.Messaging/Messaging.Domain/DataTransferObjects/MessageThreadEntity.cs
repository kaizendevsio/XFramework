using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects
{
    public partial class MessageThreadEntity
    {
        public MessageThreadEntity()
        {
            MessageThreads = new HashSet<MessageThread>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = null!;
        public string Guid { get; set; } = null!;
        public long MessageTypeId { get; set; }

        public virtual MessageType MessageType { get; set; } = null!;
        public virtual ICollection<MessageThread> MessageThreads { get; set; }
    }
}
