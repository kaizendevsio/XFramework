namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class DeletePost : BaseAction
    {
        public Guid? ContentGuid { get; set; }
    }
}