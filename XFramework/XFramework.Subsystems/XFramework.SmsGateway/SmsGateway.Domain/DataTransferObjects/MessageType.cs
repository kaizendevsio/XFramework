using System;
using System.Collections.Generic;

namespace SmsGateway.Domain.DataTransferObjects
{
    public partial class MessageType
    {
        public MessageType()
        {
            MessageDirects = new HashSet<MessageDirect>();
            MessageThreadEntities = new HashSet<MessageThreadEntity>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; } = null!;
        public short Priority { get; set; }
        public string Guid { get; set; } = null!;

        public virtual ICollection<MessageDirect> MessageDirects { get; set; }
        public virtual ICollection<MessageThreadEntity> MessageThreadEntities { get; set; }
    }
}
