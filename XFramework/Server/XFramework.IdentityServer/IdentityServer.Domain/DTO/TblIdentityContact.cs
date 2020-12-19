using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblIdentityContact
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? UserInfoId { get; set; }
        public long? UcentitiesId { get; set; }
        public string Value { get; set; }

        public virtual TblIdentityContactEntity Ucentities { get; set; }
        public virtual TblIdentityInfo UserInfo { get; set; }
    }
}
