﻿using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblIdentityRole
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

        public virtual TblIdentityRoleEntity RoleEntity { get; set; }
        public virtual TblIdentityCredential UserCred { get; set; }
    }
}
