using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CommunityConnectionType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityConnection> CommunityConnections { get; set; } = new List<CommunityConnection>();
}