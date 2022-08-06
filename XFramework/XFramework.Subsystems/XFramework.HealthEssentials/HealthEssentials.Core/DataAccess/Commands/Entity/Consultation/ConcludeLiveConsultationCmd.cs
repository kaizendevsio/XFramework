using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

public class ConcludeLiveConsultationCmd : ConcludeLiveConsultationRequest, IRequest<CmdResponse<ConcludeLiveConsultationCmd>>
{
    
}