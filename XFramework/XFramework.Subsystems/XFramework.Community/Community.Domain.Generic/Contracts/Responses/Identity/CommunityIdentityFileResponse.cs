using Community.Domain.Generic.Contracts.Responses.Storage;

namespace Community.Domain.Generic.Contracts.Responses.Identity;

public class CommunityIdentityFileResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Guid { get; set; }
    
    public virtual CommunityIdentityFileEntityResponse? Entity { get; set; }
    public virtual StorageFileResponse? Storage { get; set; }
}