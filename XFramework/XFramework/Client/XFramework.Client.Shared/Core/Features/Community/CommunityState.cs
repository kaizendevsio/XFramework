using Community.Domain.Generic.Contracts.Responses.Content;
using Community.Domain.Generic.Contracts.Responses.Identity;

namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState : State<CommunityState>
{
    public override void Initialize()
    {
        
    }

    public DateTime LastPull { get; set; } = DateTime.Now;
    public List<CommunityContentResponse> ProfileWallContentList { get; set; } = new();
    public List<CommunityContentResponse> NewsFeedContentList { get; set; } = new();
    public List<CommunityIdentityResponse> CommunityGroupList { get; set; } = new();
    public CommunityContentResponse CurrentCommunityContent { get; set; } = new();
    public CommunityIdentityResponse Identity { get; set; } = new();
    public CommunityIdentityResponse CurrentCommunityIdentity { get; set; } = new();
    public CommunityIdentityResponse CurrentCommunityGroup { get; set; } = new();
}