namespace XFramework.Domain.Generic.Contracts;

public partial record EloadPromo : BaseModel
{
    public string? Name { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<EloadProductCode> TelcoProductCodes { get; } = new List<EloadProductCode>();
}