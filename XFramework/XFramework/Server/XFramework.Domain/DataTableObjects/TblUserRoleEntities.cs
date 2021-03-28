using System;
using System.Collections.Generic;

namespace XFramework.Domain.DataTableObjects
{
    public partial class TblUserRoleEntities
    {
        public TblUserRoleEntities()
        {
            TblUserRoles = new HashSet<TblUserRoles>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
        public short? RoleLevel { get; set; }

        public virtual ICollection<TblUserRoles> TblUserRoles { get; set; }
    }
}
