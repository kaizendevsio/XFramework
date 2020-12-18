using System;
using System.Collections.Generic;

namespace XFramework.Domain.DTO
{
    public partial class TblUserInfo
    {
        public TblUserInfo()
        {
            TblAddresses = new HashSet<TblAddresses>();
            TblUserContacts = new HashSet<TblUserContacts>();
            TblUserCredentials = new HashSet<TblUserCredentials>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Uid { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public short Gender { get; set; }
        public bool IsVerified { get; set; }
        public short CivilStatus { get; set; }

        public virtual ICollection<TblAddresses> TblAddresses { get; set; }
        public virtual ICollection<TblUserContacts> TblUserContacts { get; set; }
        public virtual ICollection<TblUserCredentials> TblUserCredentials { get; set; }
    }
}
