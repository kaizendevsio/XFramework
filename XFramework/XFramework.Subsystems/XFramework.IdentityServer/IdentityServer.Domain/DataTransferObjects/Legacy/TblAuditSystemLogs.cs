using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblAuditSystemLogs
    {
        public long Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public int? Type { get; set; }
        public int? Priority { get; set; }
        public long? TaggedUserId { get; set; }

        public virtual TblUserInfo TaggedUser { get; set; }
    }
}
