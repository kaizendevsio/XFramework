

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

public class UnitEntityResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Guid { get; set; } = null!;
    public int? SortOrder { get; set; }

    public UnitEntityGroupResponse Group { get; set; } = null!;
}