using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class TblSessionDatum
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? SessionEntityId { get; set; }
        public long? UserCredentialId { get; set; }
        public string SessionData { get; set; }

        public virtual TblSessionEntity SessionEntity { get; set; }
        public virtual TblIdentityCredential UserCredential { get; set; }
    }
}
