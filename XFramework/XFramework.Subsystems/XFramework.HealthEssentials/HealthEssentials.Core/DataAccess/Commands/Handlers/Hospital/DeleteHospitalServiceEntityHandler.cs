using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalServiceEntityHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalServiceEntityCmd, CmdResponse<DeleteHospitalServiceEntityCmd>>
{
    public DeleteHospitalServiceEntityHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteHospitalServiceEntityCmd>> Handle(DeleteHospitalServiceEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}