namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetGroupList : BaseAction
    {
        public int Limit { get; set; }
    }
}