using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects
{
    public partial class CommunityIdentity
    {
        public CommunityIdentity()
        {
            CommunityConnectionSourceSocialMediaIdentities = new HashSet<CommunityConnection>();
            CommunityConnectionTargetSocialMediaIdentities = new HashSet<CommunityConnection>();
            CommunityContentCommunityGroups = new HashSet<CommunityContent>();
            CommunityContentReactions = new HashSet<CommunityContentReaction>();
            CommunityContentSocialMediaIdentities = new HashSet<CommunityContent>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public long? IdentityCredentialId { get; set; }
        public string? HandleName { get; set; }
        public int Status { get; set; }
        public DateTime LastActive { get; set; }
        public string Guid { get; set; } = null!;
        public long EntityId { get; set; }
        public string? Alias { get; set; }
        public string? Tagline { get; set; }

        public virtual CommunityIdentityEntity Entity { get; set; } = null!;
        public virtual IdentityCredential? IdentityCredential { get; set; }
        public virtual ICollection<CommunityConnection> CommunityConnectionSourceSocialMediaIdentities { get; set; }
        public virtual ICollection<CommunityConnection> CommunityConnectionTargetSocialMediaIdentities { get; set; }
        public virtual ICollection<CommunityContent> CommunityContentCommunityGroups { get; set; }
        public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; set; }
        public virtual ICollection<CommunityContent> CommunityContentSocialMediaIdentities { get; set; }
    }
}
