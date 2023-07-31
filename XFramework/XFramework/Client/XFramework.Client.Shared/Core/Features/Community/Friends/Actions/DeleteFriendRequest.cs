namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class DeleteFriendRequest : BaseAction
    {
        public Guid? Guid { get; set; }
    }
}