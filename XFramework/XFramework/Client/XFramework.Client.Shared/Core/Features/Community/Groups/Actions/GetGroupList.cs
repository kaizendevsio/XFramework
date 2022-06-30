namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupList : IAction
    {
        public int Limit { get; set; }
    }
}