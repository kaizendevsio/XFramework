

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

public class UnitResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public string Guid { get; set; } = null!;

    public UnitEntityResponse Entity { get; set; } = null!;
}