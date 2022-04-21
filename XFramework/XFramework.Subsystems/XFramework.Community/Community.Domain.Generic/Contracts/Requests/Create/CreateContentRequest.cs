using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Create;

public class CreateContentRequest : RequestBase
{
    public string? Title { get; set; }
    public string? Text { get; set; }
    public Guid? CommunityIdentityGuid { get; set; }
    public Guid? ContentEntityGuid { get; set; }
    public Guid? ParentContentGuid { get; set; }
    public Guid? CommunityGroupGuid { get; set; }
    public Guid? Guid { get; set; }
}