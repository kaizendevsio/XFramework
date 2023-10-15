namespace XFramework.Domain.Generic.Contracts;

public partial record StorageFileIdentifier : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public virtual StorageFileIdentifierGroup Group { get; set; } = null!;

    public virtual ICollection<StorageFile> StorageFiles { get; } = new List<StorageFile>();
}