namespace XFramework.Domain.Generic.Contracts;

public partial record CommunityContentType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityContent> CommunityContents { get; } = new List<CommunityContent>();
}