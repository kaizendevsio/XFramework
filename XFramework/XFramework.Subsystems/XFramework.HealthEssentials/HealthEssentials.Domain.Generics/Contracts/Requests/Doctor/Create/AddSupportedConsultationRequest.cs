using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

public class AddSupportedConsultationRequest : RequestBase
{
    public Guid? ConsultationGuid { get; set; }
    public Guid? DoctorGuid { get; set; }
    
    public decimal Price { get; set; }
    public decimal MaxDiscount { get; set; }
    public decimal Quantity { get; set; }
}