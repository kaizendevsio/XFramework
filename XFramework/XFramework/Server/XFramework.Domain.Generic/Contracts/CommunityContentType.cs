namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityContentType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityContent> CommunityContents { get; set; } = new List<CommunityContent>();
}