using Community.Domain.Generic.Contracts.Responses.Content;
using Community.Domain.Generic.Contracts.Responses.Identity;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class SetState : BaseAction
    {
        public DateTime LastPull { get; set; }
        public List<CommunityContentResponse> ProfileWallContentList { get; set; }
        public List<CommunityContentResponse> NewsFeedContentList { get; set; }
        public List<CommunityIdentityResponse> CommunityGroupList { get; set; }
        public CommunityContentResponse CurrentCommunityContent { get; set; }
        public CommunityIdentityResponse Identity { get; set; }
        public List<CommunityIdentityResponse> FriendSuggestionList { get; set; } 
        public CommunityIdentityResponse CurrentCommunityIdentity { get; set; }
        public CommunityIdentityResponse CurrentCommunityGroup { get; set; }
    }
}