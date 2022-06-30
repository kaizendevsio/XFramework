using Community.Domain.Generic.Contracts.Responses.Identity;

namespace Community.Domain.Generic.Contracts.Responses.Connection;

public class CommunityConnectionResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? Guid { get; set; }
    
    public virtual CommunityConnectionEntityResponse? Entity { get; set; }
    public virtual CommunityIdentityResponse? SourceCommunityIdentity { get; set; }
    public virtual CommunityIdentityResponse? TargetCommunityIdentity { get; set; }
}