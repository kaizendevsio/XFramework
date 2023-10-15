namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MetaDataType : BaseModel
{
    public string Name { get; set; } = null!;


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual MetaDataTypeGroup Group { get; set; } = null!;

    public virtual ICollection<MetaData> MetaData { get; } = new List<MetaData>();
}