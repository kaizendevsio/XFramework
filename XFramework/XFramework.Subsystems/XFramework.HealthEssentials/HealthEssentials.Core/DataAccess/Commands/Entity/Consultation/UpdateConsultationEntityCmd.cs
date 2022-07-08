using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

public class UpdateConsultationEntityCmd : UpdateConsultationEntityRequest, IRequest<CmdResponse<UpdateConsultationEntityCmd>>
{
    
}