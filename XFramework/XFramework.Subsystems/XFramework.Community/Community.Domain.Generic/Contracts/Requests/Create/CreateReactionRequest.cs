using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Create;

public class CreateReactionRequest : RequestBase
{
    public Guid? ContentGuid { get; set; }
    public Guid? ReactionEntityGuid { get; set; }
    public Guid? CommunityIdentityGuid { get; set; }
}