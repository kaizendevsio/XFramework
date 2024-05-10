namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class SessionType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }


    [MemoryPackOrder(1)]
    public virtual ICollection<Session> SessionData { get; set; } = new List<Session>();
}
