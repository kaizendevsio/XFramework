namespace XFramework.Domain.Generic.Contracts;

public partial class EloadPromo : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<EloadProductCode> TelcoProductCodes { get; } = new List<EloadProductCode>();
}