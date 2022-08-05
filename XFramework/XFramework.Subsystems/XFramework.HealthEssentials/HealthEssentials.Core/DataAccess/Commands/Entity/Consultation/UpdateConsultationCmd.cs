using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

public class UpdateConsultationCmd : UpdateConsultationRequest, IRequest<CmdResponse<UpdateConsultationCmd>>
{
    
}