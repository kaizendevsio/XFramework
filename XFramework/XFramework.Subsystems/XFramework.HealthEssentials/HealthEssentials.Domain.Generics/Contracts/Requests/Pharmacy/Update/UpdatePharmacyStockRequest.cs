namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

public class UpdatePharmacyStockRequest : RequestBase
{
    public Guid? PharmacyGuid { get; set; }
    public Guid? MedicineGuid { get; set; }
    public DateTime LastRestock { get; set; }
    public long? AvailableQuantity { get; set; }
    public long? CriticalQuantity { get; set; }
    public long? MinQuantity { get; set; }
    public long? MaxQuantity { get; set; }
    public long? Unit { get; set; }
}