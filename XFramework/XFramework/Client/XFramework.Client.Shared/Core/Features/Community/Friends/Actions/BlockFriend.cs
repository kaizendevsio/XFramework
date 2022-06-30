namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class BlockFriend : IAction
    {
        public Guid? CommunityIdentityGuid { get; set; }
    }
}