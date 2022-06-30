using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects
{
    public partial class IdentityRoleEntity
    {
        public IdentityRoleEntity()
        {
            IdentityRoles = new HashSet<IdentityRole>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string? Name { get; set; }
        public short? RoleLevel { get; set; }
        public string? Guid { get; set; }
        public long ApplicationId { get; set; }
        public long? GroupId { get; set; }

        public virtual Application Application { get; set; } = null!;
        public virtual IdentityRoleEntityGroup? Group { get; set; }
        public virtual ICollection<IdentityRole> IdentityRoles { get; set; }
    }
}
