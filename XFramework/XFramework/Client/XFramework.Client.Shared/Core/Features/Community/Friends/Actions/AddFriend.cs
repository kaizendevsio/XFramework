namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class AddFriend : IAction
    {
        public Guid? CommunityIdentityGuid { get; set; }
    }
}