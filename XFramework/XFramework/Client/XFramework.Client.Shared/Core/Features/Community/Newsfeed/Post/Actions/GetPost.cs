namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class GetPost : IAction
    {
        public Guid? ContentGuid { get; set; }
    }
}