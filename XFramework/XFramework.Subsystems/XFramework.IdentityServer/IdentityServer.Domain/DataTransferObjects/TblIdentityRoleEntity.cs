using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class TblIdentityRoleEntity
    {
        public TblIdentityRoleEntity()
        {
            TblIdentityRoles = new HashSet<TblIdentityRole>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
        public short? RoleLevel { get; set; }

        public virtual ICollection<TblIdentityRole> TblIdentityRoles { get; set; }
    }
}
