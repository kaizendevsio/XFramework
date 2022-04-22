using Community.Domain.Generic.Contracts.Responses.Identity;

namespace Community.Domain.Generic.Contracts.Responses.Content;

public class CommunityContentResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Title { get; set; }
    public string? Text { get; set; }
    public Guid? SocialMediaIdentityGuid { get; set; }
    public long EntityId { get; set; }
    public long ParentContentId { get; set; }
    public Guid Guid { get; set; }

    public CommunityIdentityResponse? CommunityIdentity { get; set; }
    public CommunityContentEntityResponse? Entity { get; set; }
    public List<CommunityContentFileResponse>? Files { get; set; }
    public List<CommunityContentResponse>? Comments { get; set; }
    public List<CommunityContentReactionResponse>? Reactions { get; set; }
}