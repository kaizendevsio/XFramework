using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

public class CreateConsultationEntityCmd : CreateConsultationTypeRequest, IRequest<CmdResponse<CreateConsultationEntityCmd>>
{
}