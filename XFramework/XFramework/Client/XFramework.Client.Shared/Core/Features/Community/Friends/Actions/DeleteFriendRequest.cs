namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class DeleteFriendRequest : IAction
    {
        public Guid? Guid { get; set; }
    }
}