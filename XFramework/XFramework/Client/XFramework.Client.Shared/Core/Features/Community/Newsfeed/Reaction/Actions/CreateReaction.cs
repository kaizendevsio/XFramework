namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class CreateReaction : BaseAction
    {
        public Guid? ContentGuid { get; set; }
        public Guid? ReactionEntityGuid { get; set; }
    }
}