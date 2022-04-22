namespace Community.Domain.Generic.Contracts.Responses.Connection;

public class CommunityConnectionResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? SourceCommunityIdentityGuid { get; set; }
    public Guid? TargetCommunityIdentityGuid { get; set; }
    public Guid? Guid { get; set; }
    
    public virtual CommunityConnectionEntityResponse? Entity { get; set; }
}