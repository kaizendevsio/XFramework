namespace XFramework.Domain.Generic.Contracts;

public partial record CommunityContentFile : BaseModel
{
    public Guid ContentId { get; set; }

    public Guid StorageId { get; set; }


    public virtual CommunityContent Content { get; set; } = null!;

    public virtual StorageFile Storage { get; set; } = null!;
}