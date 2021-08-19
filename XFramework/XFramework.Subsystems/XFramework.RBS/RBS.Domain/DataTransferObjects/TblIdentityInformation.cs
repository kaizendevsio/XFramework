using System;
using System.Collections.Generic;

#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class TblIdentityInformation
    {
        public TblIdentityInformation()
        {
            TblIdentityAddresses = new HashSet<TblIdentityAddress>();
            TblIdentityCredentials = new HashSet<TblIdentityCredential>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Uuid { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string IdentityName { get; set; }
        public string IdentityDescription { get; set; }
        public DateTime BirthDate { get; set; }
        public short Gender { get; set; }
        public bool IsVerified { get; set; }
        public short? CivilStatus { get; set; }

        public virtual ICollection<TblIdentityAddress> TblIdentityAddresses { get; set; }
        public virtual ICollection<TblIdentityCredential> TblIdentityCredentials { get; set; }
    }
}
