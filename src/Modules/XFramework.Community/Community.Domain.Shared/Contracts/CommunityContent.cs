using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/community-content",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "community-content"
)]
public partial class CommunityContent : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Title { get; set; }

    [MemoryPackOrder(1)]
    public string? Text { get; set; }

    [MemoryPackOrder(2)]
    public Guid SocialMediaIdentityId { get; set; }

    [MemoryPackOrder(3)]
    public Guid TypeId { get; set; }

    [MemoryPackOrder(4)]
    public Guid? ParentContentId { get; set; }


    [MemoryPackOrder(5)]
    public Guid CommunityGroupId { get; set; }

    [MemoryPackOrder(6)]
    public virtual ICollection<CommunityContentFile> CommunityContentFiles { get; set; } = new List<CommunityContentFile>();

    [MemoryPackOrder(7)]
    public virtual ICollection<CommunityContentReaction> CommunityContentReactions { get; set; } =
        new List<CommunityContentReaction>();

    [MemoryPackOrder(8)]
    public virtual CommunityIdentity? CommunityGroup { get; set; }

    [MemoryPackOrder(9)]
    public virtual CommunityContentType Type { get; set; } = null!;

    [MemoryPackOrder(10)]
    public virtual ICollection<CommunityContent> InverseParentContent { get; set; } = new List<CommunityContent>();

    [MemoryPackOrder(11)]
    public virtual CommunityContent? ParentContent { get; set; }

    [MemoryPackOrder(12)]
    public virtual CommunityIdentity SocialMediaIdentity { get; set; } = null!;
}

public class CreateCommunityContentRequest
{
    public string? Title { get; set; }
    public string? Text { get; set; }
    public Guid SocialMediaIdentityId { get; set; }
    public Guid TypeId { get; set; }
    public Guid? ParentContentId { get; set; }
    public Guid CommunityGroupId { get; set; }
}

public class UpdateCommunityContentRequest
{
    public string? Title { get; set; }
    public string? Text { get; set; }
    public Guid SocialMediaIdentityId { get; set; }
    public Guid TypeId { get; set; }
    public Guid? ParentContentId { get; set; }
    public Guid CommunityGroupId { get; set; }
}

public class GetCommunityContentListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? SocialMediaIdentityId { get; set; }
    public Guid? TypeId { get; set; }
    public Guid? ParentContentId { get; set; }
    public Guid? CommunityGroupId { get; set; }
}
