namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityIdentityFileType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; set; } =
        new List<CommunityIdentityFile>();
}
