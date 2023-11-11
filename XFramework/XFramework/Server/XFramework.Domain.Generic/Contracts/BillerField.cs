namespace XFramework.Domain.Generic.Contracts;

public partial class BillerField : BaseModel
{
    public Guid BillerId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string Type { get; set; } = null!;

    public decimal? Width { get; set; }


    public virtual Biller Biller { get; set; } = null!;
}