namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class UpdateReaction : BaseAction
    {
        public Guid? Guid { get; set; }
        public Guid? ContentGuid { get; set; }
        public Guid? ReactionEntityGuid { get; set; }
    }
}