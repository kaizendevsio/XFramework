using System;
using System.Collections.Generic;

namespace XFramework.Domain.DTO
{
    public partial class TblSessionData
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

        public virtual TblSessionEntities SessionEntity { get; set; }
        public virtual TblUserCredentials UserCredential { get; set; }
    }
}
