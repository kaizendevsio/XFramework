using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class MetaDataType : BaseModel
{
    public string Name { get; set; } = null!;

    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual MetaDataTypeGroup Group { get; set; } = null!;

    public virtual ICollection<MetaData> MetaData { get; set; } = new List<MetaData>();
}