namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupMemberList : BaseAction
    {
        public int Limit { get; set; }
    }
}