namespace XFramework.Domain.Generic.Contracts;

public partial record EloadTelcoPrefix : BaseModel
{
    public Guid? TelcoTypeId { get; set; }

    public string? Prefix { get; set; }


    public virtual EloadTelcoType? TelcoType { get; set; }
}