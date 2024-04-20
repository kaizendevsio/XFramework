using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CommunityIdentityFileType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityIdentityFile> CommunityIdentityFiles { get; set; } =
        new List<CommunityIdentityFile>();
}