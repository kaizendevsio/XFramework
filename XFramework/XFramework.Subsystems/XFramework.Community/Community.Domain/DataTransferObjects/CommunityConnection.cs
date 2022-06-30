using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class CommunityConnection
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long SourceSocialMediaIdentityId { get; set; }
        public long TargetSocialMediaIdentityId { get; set; }
        public long EntityId { get; set; }
        public string Guid { get; set; } = null!;

        public virtual CommunityConnectionEntity Entity { get; set; } = null!;
        public virtual CommunityIdentity SourceSocialMediaIdentity { get; set; } = null!;
        public virtual CommunityIdentity TargetSocialMediaIdentity { get; set; } = null!;
    }
}
