namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityIdentityFileType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; set; } =
        new List<CommunityIdentityFile>();
}