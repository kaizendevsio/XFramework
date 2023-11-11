namespace XFramework.Domain.Generic.Contracts;

public partial class EloadTelcoPrefix : BaseModel
{
    public Guid? TelcoTypeId { get; set; }

    public string? Prefix { get; set; }


    public virtual EloadTelcoType? TelcoType { get; set; }
}