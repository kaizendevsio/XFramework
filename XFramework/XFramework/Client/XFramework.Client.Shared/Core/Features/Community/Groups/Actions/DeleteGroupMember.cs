namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class DeleteGroupMember : BaseAction
    {
        public Guid? Guid { get; set; }
    }
}