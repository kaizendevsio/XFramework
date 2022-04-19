using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class CommunityContentFile
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long ContentId { get; set; }
        public long StorageId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual CommunityContent Content { get; set; } = null!;
    }
}
