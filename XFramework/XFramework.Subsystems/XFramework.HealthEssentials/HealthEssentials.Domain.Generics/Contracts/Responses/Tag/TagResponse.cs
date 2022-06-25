

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

public class TagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public string Guid { get; set; } = null!;

    public TagEntityResponse Entity { get; set; } = null!;
}