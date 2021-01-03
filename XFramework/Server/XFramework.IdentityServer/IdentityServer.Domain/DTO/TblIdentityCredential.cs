using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblIdentityCredential
    {
        public TblIdentityCredential()
        {
            TblAuthorizationLogs = new HashSet<TblAuthorizationLog>();
            TblIdentityContacts = new HashSet<TblIdentityContact>();
            TblIdentityRoles = new HashSet<TblIdentityRole>();
            TblIdentityVerifications = new HashSet<TblIdentityVerification>();
            TblSessionData = new HashSet<TblSessionDatum>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? IdentityInfoId { get; set; }
        public string UserName { get; set; }
        public string UserAlias { get; set; }
        public short? LogInStatus { get; set; }
        public byte[] PasswordByte { get; set; }
        public long AppId { get; set; }

        public virtual TblApplication App { get; set; }
        public virtual TblIdentityInfo IdentityInfo { get; set; }
        public virtual ICollection<TblAuthorizationLog> TblAuthorizationLogs { get; set; }
        public virtual ICollection<TblIdentityContact> TblIdentityContacts { get; set; }
        public virtual ICollection<TblIdentityRole> TblIdentityRoles { get; set; }
        public virtual ICollection<TblIdentityVerification> TblIdentityVerifications { get; set; }
        public virtual ICollection<TblSessionDatum> TblSessionData { get; set; }
    }
}
