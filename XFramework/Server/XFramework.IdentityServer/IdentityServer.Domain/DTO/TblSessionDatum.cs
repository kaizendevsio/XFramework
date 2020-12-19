using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblSessionDatum
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? SessionEntityId { get; set; }
        public long? UserCredentialId { get; set; }
        public string SessionData { get; set; }

        public virtual TblSessionEntity SessionEntity { get; set; }
        public virtual TblIdentityCredential UserCredential { get; set; }
    }
}
