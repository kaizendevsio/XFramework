using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using XFramework.Domain.Shared.Attributes;

namespace IdentityServer.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/identity-info",
    RequireAuthorization = true,
    CacheDurationSeconds = 300,
    CacheKeyPrefix = "identity-info"
)]
public partial class IdentityInformation : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? FirstName { get; set; }

    [MemoryPackOrder(1)]
    public string? MiddleName { get; set; }

    [MemoryPackOrder(2)]
    public string? LastName { get; set; }

    [MemoryPackOrder(3)]
    public string? Suffix { get; set; }
    
    [NotMapped]
    [JsonIgnore]
    [MemoryPackIgnore]
    public string? FullName => string.Join(" ", FirstName, MiddleName, LastName, Suffix);
    
    [MemoryPackOrder(4)]
    public string? IdentityName { get; set; }

    [MemoryPackOrder(5)]
    public string? IdentityDescription { get; set; }

    [MemoryPackOrder(6)]
    public DateOnly? BirthDate { get; set; }

    [MemoryPackOrder(7)]
    public Gender? Gender { get; set; }

    [MemoryPackOrder(8)]
    public bool IsVerified { get; set; }

    [MemoryPackOrder(9)]
    public CivilStatus? CivilStatus { get; set; }


    [MemoryPackOrder(10)]
    public virtual Tenant Tenant { get; set; } = null!;

    [MemoryPackOrder(11)]
    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();

    [MemoryPackOrder(12)]
    public virtual ICollection<IdentityCredential> IdentityCredentials { get; set; } = new List<IdentityCredential>();
}

public class CreateIdentityInformationRequest
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Suffix { get; set; }
    public string? IdentityName { get; set; }
    public string? IdentityDescription { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public bool IsVerified { get; set; }
    public CivilStatus? CivilStatus { get; set; }
}

public class UpdateIdentityInformationRequest
{
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Suffix { get; set; }
    public string? IdentityName { get; set; }
    public string? IdentityDescription { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public bool IsVerified { get; set; }
    public CivilStatus? CivilStatus { get; set; }
}

public class GetIdentityInformationListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Gender? Gender { get; set; }
    public CivilStatus? CivilStatus { get; set; }
    public bool? IsVerified { get; set; }
}
