using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class DeleteHospitalServiceCmd : DeleteHospitalServiceRequest, IRequest<CmdResponse<DeleteHospitalServiceCmd>>
{
    
}