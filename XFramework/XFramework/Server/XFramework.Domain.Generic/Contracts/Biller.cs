namespace XFramework.Domain.Generic.Contracts;

public partial class Biller : BaseModel
{
    public Guid BillerCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal ServiceCharge { get; set; }

    public string? Image { get; set; }


    public Guid? ProviderEndpointId { get; set; }

    public decimal? Discount { get; set; }

    public decimal ConvenienceFee { get; set; }


    public virtual BillerCategory BillerCategory { get; set; } = null!;

    public virtual ICollection<BillerField> BillerFields { get; } = new List<BillerField>();

    public virtual ICollection<BillsPaymentTransaction> BillsPaymentTransactions { get; } =
        new List<BillsPaymentTransaction>();

    public virtual ProviderEndpoint? ProviderEndpoint { get; set; }
}