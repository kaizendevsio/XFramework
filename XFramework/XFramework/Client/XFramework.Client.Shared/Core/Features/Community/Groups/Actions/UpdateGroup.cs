namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class UpdateGroup : IAction
    {
        public Guid? Guid { get; set; }
        public string Name { get; set; }
        public string HandleName { get; set; }
        public string Tagline { get; set; }
    }
}