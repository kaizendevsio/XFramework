namespace XFramework.Domain.Generic.Contracts;

public partial class SessionType : BaseModel
{
    public string? Name { get; set; }


    public virtual ICollection<SessionDatum> SessionData { get; } = new List<SessionDatum>();
}