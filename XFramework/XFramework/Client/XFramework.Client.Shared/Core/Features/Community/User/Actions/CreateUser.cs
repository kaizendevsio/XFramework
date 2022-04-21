namespace XFramework.Client.Shared.Core.Features.User;

public partial class UserState
{
    public class CreateUser : IAction
    {
        public Guid? CredentialGuid { get; set; }
        public Guid? CommunityEntityGuid { get; set; }
    }
}