namespace XFramework.Domain.Generic.Contracts;

public partial class StorageFileIdentifierGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<StorageFileIdentifier> StorageFileIdentifiers { get; } =
        new List<StorageFileIdentifier>();
}