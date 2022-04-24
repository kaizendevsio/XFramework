using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects
{
    public partial class IdentityRole
    {
        public IdentityRole()
        {
            MessageThreadMemberRoles = new HashSet<MessageThreadMemberRole>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? UserCredId { get; set; }
        public long? RoleEntityId { get; set; }
        public DateTime RoleExpiration { get; set; }
        public string? Guid { get; set; }

        public virtual IdentityRoleEntity? RoleEntity { get; set; }
        public virtual IdentityCredential? UserCred { get; set; }
        public virtual ICollection<MessageThreadMemberRole> MessageThreadMemberRoles { get; set; }
    }
}
