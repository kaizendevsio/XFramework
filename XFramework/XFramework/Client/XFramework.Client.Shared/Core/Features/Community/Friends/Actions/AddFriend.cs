namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class AddFriend : BaseAction
    {
        public Guid? CommunityIdentityGuid { get; set; }
    }
}