namespace XFramework.Domain.Generic.Contracts;

public partial class EloadGatewayNumber : BaseModel
{
    public Guid? TelcoTypeId { get; set; }

    public string? Gateway { get; set; }


    public virtual EloadTelcoType? EloadTelcoType { get; set; }
}