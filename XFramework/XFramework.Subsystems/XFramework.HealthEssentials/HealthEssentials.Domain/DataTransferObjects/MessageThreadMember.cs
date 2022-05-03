using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects
{
    public partial class MessageThreadMember
    {
        public MessageThreadMember()
        {
            MessageDeliveries = new HashSet<MessageDelivery>();
            MessageThreadMemberRoles = new HashSet<MessageThreadMemberRole>();
            Messages = new HashSet<Message>();
        }

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
        public virtual MessageThread MessageThread { get; set; } = null!;
        public virtual ICollection<MessageDelivery> MessageDeliveries { get; set; }
        public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; set; }
        public virtual ICollection<Message> Messages { get; set; }
    }
}
