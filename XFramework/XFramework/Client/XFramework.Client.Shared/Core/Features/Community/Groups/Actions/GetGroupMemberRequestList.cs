namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupMemberRequestList : IAction
    {
        public int Limit { get; set; }
    }
}