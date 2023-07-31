namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class CreateGroup : BaseAction
    {
        public string Name { get; set; }
        public string HandleName { get; set; }
        public string Tagline { get; set; }
        public string NavigateToOnSuccess { get; set; }
        public string NavigateToOnFailure { get; set; }
    }
}