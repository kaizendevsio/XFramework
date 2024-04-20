using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class MetaDataTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;

    public virtual ICollection<MetaDataType> MetaDataTypes { get; set; } = new List<MetaDataType>();
}