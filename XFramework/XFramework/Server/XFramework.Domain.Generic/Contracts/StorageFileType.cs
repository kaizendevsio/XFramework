namespace XFramework.Domain.Generic.Contracts;

public partial record StorageFileType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<StorageFile> StorageFiles { get; } = new List<StorageFile>();
}