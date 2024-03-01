namespace XFramework.Domain.Generic.Contracts;

public partial class CommunityConnectionType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityConnection> CommunityConnections { get; set; } = new List<CommunityConnection>();
}