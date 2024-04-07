using HealthEssentials.Domain.Generics.Abstractions;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyStock : BaseModel, IHasCost, IHasPrice
{
    public Guid PharmacyBranchId { get; set; }

    public Guid MedicineVariantId { get; set; }

    public DateTime LastRestock { get; set; }
    public DateTime? NearestExpiryDate { get; set; }

    public decimal AvailableQuantity { get; set; }

    public decimal CriticalQuantity { get; set; }

    public decimal MinQuantity { get; set; }

    public decimal MaxQuantity { get; set; }

    public Guid UnitId { get; set; }

    public virtual MedicineVariant MedicineVariant { get; set; } = null!;
    
    public virtual PharmacyBranch PharmacyBranch { get; set; } = null!;

    public virtual ICollection<MedicineVariant> AlternativeMedicineVariants { get; set; } = [];
    
    public virtual Unit? Unit { get; set; }
    
    public decimal CostEx { get; set; }
    public decimal CostInc { get; set; }
    public decimal CostTax => CostInc - CostEx;
    public decimal PriceEx { get; set; }
    public decimal PriceInc { get; set; }
    public decimal PriceTax => PriceInc - PriceEx;

}