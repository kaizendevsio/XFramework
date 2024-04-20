using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CommunityIdentityType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityIdentity> CommunityIdentities { get; set; } = new List<CommunityIdentity>();
}