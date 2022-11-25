namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Update;

public class UpdatePharmacyJobOrderConsultationJobOrderRequest : RequestBase
{
    public Guid? PharmacyJobOrderGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
}