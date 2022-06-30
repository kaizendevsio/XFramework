namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class CreateReaction : IAction
    {
        public Guid? ContentGuid { get; set; }
        public Guid? ReactionEntityGuid { get; set; }
    }
}