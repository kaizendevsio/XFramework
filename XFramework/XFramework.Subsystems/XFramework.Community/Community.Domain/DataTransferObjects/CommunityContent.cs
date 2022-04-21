using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class CommunityContent
    {
        public CommunityContent()
        {
            CommunityContentFiles = new HashSet<CommunityContentFile>();
            CommunityContentReactions = new HashSet<CommunityContentReaction>();
            InverseParentContent = new HashSet<CommunityContent>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string? Title { get; set; }
        public string? Text { get; set; }
        public long SocialMediaIdentityId { get; set; }
        public long EntityId { get; set; }
        public long ParentContentId { get; set; }
        public string Guid { get; set; } = null!;
        public long? CommunityGroupId { get; set; }

        public virtual CommunityIdentity? CommunityGroup { get; set; }
        public virtual CommunityContentEntity Entity { get; set; } = null!;
        public virtual CommunityContent ParentContent { get; set; } = null!;
        public virtual CommunityIdentity SocialMediaIdentity { get; set; } = null!;
        public virtual ICollection<CommunityContentFile> CommunityContentFiles { get; set; }
        public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; set; }
        public virtual ICollection<CommunityContent> InverseParentContent { get; set; }
    }
}
