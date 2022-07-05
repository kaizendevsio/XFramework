using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

public class DeleteConsultationEntityCmd : DeleteConsultationEntityRequest, IRequest<CmdResponse<DeleteConsultationEntityCmd>>
{
    
}