namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class AddGroupMemberRequest : BaseAction
    {
        public Guid? CommunityIdentityGuid { get; set; }
        public Guid? CommunityGroupGuid { get; set; }
    }
}