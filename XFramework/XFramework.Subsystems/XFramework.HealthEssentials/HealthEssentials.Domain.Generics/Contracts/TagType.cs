namespace HealthEssentials.Domain.Generics.Contracts;

public partial class TagType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    
    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual TagTypeGroup Group { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; } = new List<Tag>();
}
