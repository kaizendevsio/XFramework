using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

public class CreateConsultationJobOrderMedicineCmd : CreateConsultationJobOrderMedicineRequest, IRequest<CmdResponse<CreateConsultationJobOrderMedicineCmd>>
{
    
}