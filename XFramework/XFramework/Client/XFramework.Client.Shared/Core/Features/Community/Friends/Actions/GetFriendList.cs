namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetFriendList : IAction
    {
        public int Limit { get; set; }
        public Guid? CommunityIdentityGuid { get; set; }
    }
}