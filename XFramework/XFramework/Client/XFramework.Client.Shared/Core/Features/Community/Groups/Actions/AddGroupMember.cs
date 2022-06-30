namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class AddGroupMember : IAction
    {
        public Guid? CommunityIdentityGuid { get; set; }
        public Guid? CommunityGroupGuid { get; set; }
    }
}