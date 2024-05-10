namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CommunityContentType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;


    [MemoryPackOrder(1)]
    public virtual ICollection<CommunityContent> CommunityContents { get; set; } = new List<CommunityContent>();
}
