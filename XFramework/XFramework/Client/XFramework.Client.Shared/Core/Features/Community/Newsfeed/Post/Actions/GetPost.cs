namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetPost : BaseAction
    {
        public Guid? ContentGuid { get; set; }
    }
}