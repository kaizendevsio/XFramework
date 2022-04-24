namespace XFramework.Client.Shared.Core.Features.Community;

public partial class CommunityState
{
    public class CreateIdentity : IAction
    {
        public Guid? CredentialGuid { get; set; }
        public string Alias { get; set; }
        public string HandleName { get; set; }
        public string Tagline { get; set; }
    }
}