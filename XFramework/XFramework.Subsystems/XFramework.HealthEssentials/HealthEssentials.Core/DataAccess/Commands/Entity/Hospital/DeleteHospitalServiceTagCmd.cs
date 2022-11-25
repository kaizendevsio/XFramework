using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class DeleteHospitalServiceTagCmd : DeleteHospitalServiceTagRequest, IRequest<CmdResponse<DeleteHospitalServiceTagCmd>>
{
    
}