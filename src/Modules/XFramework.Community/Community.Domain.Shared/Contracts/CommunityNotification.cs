using Community.Domain.Shared.Enums;
using XFramework.Domain.Shared.Attributes;

namespace Community.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly | EndpointActions.Delete,
    RoutePrefix = "api/community-notifications",
    RequireAuthorization = true,
    CacheDurationSeconds = 60,
    CacheKeyPrefix = "community-notifications"
)]
public partial class CommunityNotification : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid RecipientIdentityId { get; set; }

    [MemoryPackOrder(1)]
    public Guid ActorIdentityId { get; set; }

    [MemoryPackOrder(2)]
    public NotificationType Type { get; set; }

    [MemoryPackOrder(3)]
    public Guid? ReferenceId { get; set; }

    [MemoryPackOrder(4)]
    public string? Message { get; set; }

    [MemoryPackOrder(5)]
    public bool IsRead { get; set; }

    [MemoryPackOrder(6)]
    public virtual CommunityIdentity RecipientIdentity { get; set; } = null!;

    [MemoryPackOrder(7)]
    public virtual CommunityIdentity ActorIdentity { get; set; } = null!;
}

public class GetCommunityNotificationListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? RecipientIdentityId { get; set; }
    public Guid? ActorIdentityId { get; set; }
    public bool? IsRead { get; set; }
}
