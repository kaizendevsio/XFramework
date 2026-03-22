using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/identity-credentials",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "identity-credentials"
)]
public partial class IdentityCredential : BaseModel, IHasOnlineStatus
{
    
    [MemoryPackOrder(0)]
    public Guid IdentityInfoId { get; set; }

    [MemoryPackOrder(1)]
    public string? UserName { get; set; }

    [MemoryPackOrder(2)]
    public string? UserAlias { get; set; }

    [MemoryPackOrder(3)]
    public short? LogInStatus { get; set; }

    [JsonIgnore]
    [MemoryPackOrder(4)]
    public byte[]? PasswordByte { get; set; }
    
    [NotMapped]
    [JsonIgnore]
    [MemoryPackOrder(5)]
    public string? Password { get; set; }

    [JsonIgnore]
    [MemoryPackOrder(6)]
    public string? Token { get; set; }
    
    [MemoryPackOrder(7)]
    public bool IsOnline { get; set; }
    
    [MemoryPackOrder(8)]
    public DateTime LastSeen { get; set; }
    
    [MemoryPackOrder(9)]
    public DateTime? OnlineSince { get; set; }
    
    [MemoryPackOrder(10)]
    public string? StatusMessage { get; set; }
    
    [MemoryPackOrder(11)]
    public string? LastActivityType { get; set; }
    
    [MemoryPackOrder(12)]
    public string? Device { get; set; }
    
    [MemoryPackOrder(13)]
    public string? Location { get; set; }
    
    
    [MemoryPackOrder(14)]
    public virtual Tenant Tenant { get; set; } = null!;

    [MemoryPackOrder(15)]
    public virtual ICollection<AuthorizationLog> AuthorizationLogs { get; set; } = new List<AuthorizationLog>();
    
    [MemoryPackOrder(18)]
    public virtual ICollection<IdentityContact> IdentityContacts { get; set; } = new List<IdentityContact>();

    [MemoryPackOrder(19)]
    public virtual ICollection<IdentityFavorite> IdentityFavorites { get; set; } = new List<IdentityFavorite>();

    [MemoryPackOrder(20)]
    public virtual IdentityInformation? IdentityInfo { get; set; }

    [MemoryPackOrder(21)]
    public virtual ICollection<IdentityRole> IdentityRoles { get; set; } = new List<IdentityRole>();

    [MemoryPackOrder(22)]
    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();
    
    [MemoryPackOrder(26)]
    public virtual ICollection<Session> SessionData { get; set; } = new List<Session>();

    [MemoryPackOrder(27)]
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

}

public class GetIdentityCredentialListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? UserName { get; set; }
    public Guid? IdentityInfoId { get; set; }
}
