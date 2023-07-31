namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

public class UpdateDoctorConsultationRequest : RequestBase
{
    public Guid? ConsultationGuid { get; set; }
    public Guid? DoctorGuid { get; set; }
    public decimal? Price { get; set; }
    public decimal? MaxDiscount { get; set; }
    public int Quantity { get; set; }
    public bool IsEnabled { get; set; }

}