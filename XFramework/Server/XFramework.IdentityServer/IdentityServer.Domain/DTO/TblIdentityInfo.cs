using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblIdentityInfo
    {
        public TblIdentityInfo()
        {
            TblIdentityAddresses = new HashSet<TblIdentityAddress>();
            TblIdentityContacts = new HashSet<TblIdentityContact>();
            TblIdentityCredentials = new HashSet<TblIdentityCredential>();
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
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string IdentityName { get; set; }
        public string IdentityDescription { get; set; }
        public DateTime Dob { get; set; }
        public short Gender { get; set; }
        public bool IsVerified { get; set; }
        public short CivilStatus { get; set; }

        public virtual ICollection<TblIdentityAddress> TblIdentityAddresses { get; set; }
        public virtual ICollection<TblIdentityContact> TblIdentityContacts { get; set; }
        public virtual ICollection<TblIdentityCredential> TblIdentityCredentials { get; set; }
    }
}
