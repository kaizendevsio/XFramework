namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetFriendRequestList : BaseAction
    {
        public int Limit { get; set; }
    }
}