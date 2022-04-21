using Community.Domain.Generic.Contracts.Responses.Identity;

namespace Community.Domain.Generic.Contracts.Responses.Content;

public class CommunityContentResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Avatar { get; set; }
    public string? Title { get; set; }
    public string? Text { get; set; }
    public long SocialMediaIdentityGuid { get; set; }
    public long EntityId { get; set; }
    public long ParentContentId { get; set; }
    public string Guid { get; set; } = null!;

    public CommunityIdentityResponse? CommunityIdentity { get; set; }
    public CommunityContentEntityResponse? ContentEntity { get; set; }
    public List<CommunityContentFileResponse>? CommunityContentFiles { get; set; }
    public List<CommunityContentReactionResponse>? CommunityContentReactions { get; set; }
}