using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalEntityHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalEntityCmd, CmdResponse<UpdateHospitalEntityCmd>>
{
    public UpdateHospitalEntityHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateHospitalEntityCmd>> Handle(UpdateHospitalEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}