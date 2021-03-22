using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class TblIdentityContact
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? UcentitiesId { get; set; }
        public string Value { get; set; }
        public long? UserCredentialId { get; set; }

        public virtual TblIdentityContactEntity Ucentities { get; set; }
        public virtual TblIdentityCredential UserCredential { get; set; }
    }
}
