namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class FollowFriend : BaseAction
    {
        public Guid? CommunityIdentityGuid { get; set; }
    }
}