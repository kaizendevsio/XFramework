namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyJobOrderConsultationJobOrderRequest : RequestBase
{
    public Guid? PharmacyJobOrderGuid { get; set; }
    public Guid? ConsultationJobOrderGuid { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
}