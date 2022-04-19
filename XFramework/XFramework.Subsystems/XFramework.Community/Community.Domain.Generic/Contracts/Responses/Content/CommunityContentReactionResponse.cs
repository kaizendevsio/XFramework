namespace Community.Domain.Generic.Contracts.Responses.Content;

public class CommunityContentReactionResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long ContentId { get; set; }
    public long EntityId { get; set; }
    public long SocialMediaIdentityId { get; set; }
    public string Guid { get; set; } = null!;
}