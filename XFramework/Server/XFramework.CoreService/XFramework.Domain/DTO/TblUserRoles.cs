using System;
using System.Collections.Generic;

namespace XFramework.Domain.DTO
{
    public partial class TblUserRoles
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? UserCredId { get; set; }
        public long? RoleEntityId { get; set; }
        public DateTime RoleExpiration { get; set; }

        public virtual TblUserRoleEntities RoleEntity { get; set; }
        public virtual TblUserCredentials UserCred { get; set; }
    }
}
