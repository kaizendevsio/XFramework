namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class DeleteComment : IAction
    {
        public Guid? ContentGuid { get; set; }
    }
}