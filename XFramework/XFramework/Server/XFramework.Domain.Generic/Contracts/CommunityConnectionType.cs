namespace XFramework.Domain.Generic.Contracts;

public partial record CommunityConnectionType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<CommunityConnection> CommunityConnections { get; } = new List<CommunityConnection>();
}