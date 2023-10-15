namespace XFramework.Domain.Generic.Contracts;

public partial record CommunityIdentityFileType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; } =
        new List<CommunityIdentityFile>();
}