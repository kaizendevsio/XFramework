using System;
using System.Collections.Generic;

namespace Community.Domain.DataTransferObjects;

public partial class CommunityContent
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string? Title { get; set; }

    public string? Text { get; set; }

    public long SocialMediaIdentityId { get; set; }

    public long EntityId { get; set; }

    public long? ParentContentId { get; set; }

    public string Guid { get; set; } = null!;

    public long? CommunityGroupId { get; set; }

    public virtual ICollection<CommunityContentFile> CommunityContentFiles { get; } = new List<CommunityContentFile>();

    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; } = new List<CommunityContentReaction>();

    public virtual CommunityIdentity? CommunityGroup { get; set; }

    public virtual CommunityContentEntity Entity { get; set; } = null!;

    public virtual ICollection<CommunityContent> InverseParentContent { get; } = new List<CommunityContent>();

    public virtual CommunityContent? ParentContent { get; set; }

    public virtual CommunityIdentity SocialMediaIdentity { get; set; } = null!;
}
