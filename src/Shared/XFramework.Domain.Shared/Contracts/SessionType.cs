using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class SessionType : BaseModel
{
    public string? Name { get; set; }


    public virtual ICollection<Session> SessionData { get; set; } = new List<Session>();
}