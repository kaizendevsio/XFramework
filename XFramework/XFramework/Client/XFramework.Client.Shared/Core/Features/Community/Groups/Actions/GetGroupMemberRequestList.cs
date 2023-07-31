namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupMemberRequestList : BaseAction
    {
        public int Limit { get; set; }
    }
}