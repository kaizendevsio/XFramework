using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class StorageFileIdentifierGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<StorageFileIdentifier> StorageFileIdentifiers { get; set; } =
        new List<StorageFileIdentifier>();
}