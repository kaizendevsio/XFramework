namespace XFramework.Domain.Generic.Contracts;

public partial record StorageFileIdentifierGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<StorageFileIdentifier> StorageFileIdentifiers { get; } =
        new List<StorageFileIdentifier>();
}