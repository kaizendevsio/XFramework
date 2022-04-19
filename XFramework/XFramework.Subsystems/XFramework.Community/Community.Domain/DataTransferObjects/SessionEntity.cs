using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class SessionEntity
    {
        public SessionEntity()
        {
            SessionData = new HashSet<SessionDatum>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string? Name { get; set; }
        public string Guid { get; set; } = null!;

        public virtual ICollection<SessionDatum> SessionData { get; set; }
    }
}
