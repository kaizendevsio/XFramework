using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects
{
    public partial class IdentityInformation
    {
        public IdentityInformation()
        {
            IdentityAddresses = new HashSet<IdentityAddress>();
            IdentityCredentials = new HashSet<IdentityCredential>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Guid { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? IdentityName { get; set; }
        public string? IdentityDescription { get; set; }
        public DateOnly BirthDate { get; set; }
        public short Gender { get; set; }
        public bool IsVerified { get; set; }
        public short? CivilStatus { get; set; }

        public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; }
        public virtual ICollection<IdentityCredential> IdentityCredentials { get; set; }
    }
}
