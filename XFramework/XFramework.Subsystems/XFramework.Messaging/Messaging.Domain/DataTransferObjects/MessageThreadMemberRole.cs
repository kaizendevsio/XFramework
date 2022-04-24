using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects
{
    public partial class MessageThreadMemberRole
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Guid { get; set; } = null!;
        public long MessageThreadMemberId { get; set; }
        public long RoleId { get; set; }

        public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
        public virtual IdentityRole Role { get; set; } = null!;
    }
}
