using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Update;

public class UpdateReactionRequest : RequestBase
{
    public Guid? Guid { get; set; }
    public Guid? ReactionEntityGuid { get; set; }
}