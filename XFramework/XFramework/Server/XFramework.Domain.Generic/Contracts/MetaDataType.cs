namespace XFramework.Domain.Generic.Contracts;

public partial class MetaDataType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual MetaDataTypeGroup Group { get; set; } = null!;

    public virtual ICollection<MetaData> MetaData { get; } = new List<MetaData>();
}
