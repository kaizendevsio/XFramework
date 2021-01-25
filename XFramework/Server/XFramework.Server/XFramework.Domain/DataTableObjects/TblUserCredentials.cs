using System;
using System.Collections.Generic;

namespace XFramework.Domain.DataTableObjects
{
    public partial class TblUserCredentials
    {
        public TblUserCredentials()
        {
            TblSessionData = new HashSet<TblSessionData>();
            TblUserAuthHistory = new HashSet<TblUserAuthHistory>();
            TblUserRoles = new HashSet<TblUserRoles>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? UserInfoId { get; set; }
        public string UserName { get; set; }
        public string UserAlias { get; set; }
        public short? LogInStatus { get; set; }
        public byte[] ResetPasswordCodeByte { get; set; }
        public byte[] PasswordByte { get; set; }
        public DateTime? ResetPasswordCodeExpiration { get; set; }
        public long? AppId { get; set; }

        public virtual TblApplication App { get; set; }
        public virtual TblUserInfo UserInfo { get; set; }
        public virtual ICollection<TblSessionData> TblSessionData { get; set; }
        public virtual ICollection<TblUserAuthHistory> TblUserAuthHistory { get; set; }
        public virtual ICollection<TblUserRoles> TblUserRoles { get; set; }
    }
}
