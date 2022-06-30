

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

public class AilmentEntityResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? Guid { get; set; }
    public long GroupId { get; set; }
    public int? SortOrder { get; set; }
    
    public virtual AilmentEntityGroupResponse Group { get; set; } = null!;
}