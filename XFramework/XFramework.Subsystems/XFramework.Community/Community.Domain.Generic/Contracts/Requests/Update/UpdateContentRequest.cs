using XFramework.Domain.Generic.Contracts.Requests;

namespace Community.Domain.Generic.Contracts.Requests.Update;

public class UpdateContentRequest : RequestBase
{
    public string? Title { get; set; }
    public string? Text { get; set; }
    public Guid? SocialMediaIdentityGuid { get; set; }
    public Guid? Guid { get; set; }
}