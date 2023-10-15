namespace XFramework.Domain.Generic.Contracts;

public partial record MetaDataTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;

    public virtual ICollection<MetaDataType> MetaDataTypes { get; } = new List<MetaDataType>();
}