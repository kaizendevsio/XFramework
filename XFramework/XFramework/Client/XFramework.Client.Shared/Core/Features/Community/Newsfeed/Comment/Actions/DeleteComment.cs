namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class DeleteComment : BaseAction
    {
        public Guid? ContentGuid { get; set; }
    }
}