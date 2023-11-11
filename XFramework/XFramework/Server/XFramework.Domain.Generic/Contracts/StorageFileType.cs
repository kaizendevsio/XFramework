namespace XFramework.Domain.Generic.Contracts;

public partial class StorageFileType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<StorageFile> StorageFiles { get; } = new List<StorageFile>();
}