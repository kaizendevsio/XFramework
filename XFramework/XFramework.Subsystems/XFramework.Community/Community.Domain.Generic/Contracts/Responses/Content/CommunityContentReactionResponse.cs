using Community.Domain.Generic.Contracts.Responses.Identity;

namespace Community.Domain.Generic.Contracts.Responses.Content;

public class CommunityContentReactionResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? Guid { get; set; }

    public CommunityContentReactionEntityResponse? Entity { get; set; }
    public CommunityIdentityResponse? CommunityIdentity { get; set; }
}