using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class StorageFileType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<StorageFile> StorageFiles { get; set; } = new List<StorageFile>();
}