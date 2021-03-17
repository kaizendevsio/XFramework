using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DataTransferObjects
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
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? IdentityInfoId { get; set; }
        public string UserName { get; set; }
        public string UserAlias { get; set; }
        public short? LogInStatus { get; set; }
        public byte[] PasswordByte { get; set; }
        public long ApplicationId { get; set; }
        public string Token { get; set; }

        public virtual TblApplication Application { get; set; }
        public virtual TblIdentityInformation IdentityInfo { get; set; }
        public virtual ICollection<TblAuthorizationLog> TblAuthorizationLogs { get; set; }
        public virtual ICollection<TblIdentityContact> TblIdentityContacts { get; set; }
        public virtual ICollection<TblIdentityRole> TblIdentityRoles { get; set; }
        public virtual ICollection<TblIdentityVerification> TblIdentityVerifications { get; set; }
        public virtual ICollection<TblSessionDatum> TblSessionData { get; set; }
    }
}
