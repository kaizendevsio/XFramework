namespace XFramework.Domain.Generic.Contracts;

public partial class EloadTelcoType : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }


    public string? Image { get; set; }


    public virtual ICollection<EloadGatewayNumber> GatewayNumbers { get; } = new List<EloadGatewayNumber>();

    public virtual ICollection<EloadTelcoPrefix> TelcoPrefixes { get; } = new List<EloadTelcoPrefix>();

    public virtual ICollection<EloadProductCode> TelcoProductCodes { get; } = new List<EloadProductCode>();
}