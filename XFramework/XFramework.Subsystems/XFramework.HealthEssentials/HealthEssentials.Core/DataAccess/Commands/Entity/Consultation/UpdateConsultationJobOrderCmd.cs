using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

public class UpdateConsultationJobOrderCmd : UpdateConsultationJobOrderRequest, IRequest<CmdResponse<UpdateConsultationJobOrderCmd>>
{
    
}