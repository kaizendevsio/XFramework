using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalLocationHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalLocationCmd, CmdResponse<UpdateHospitalLocationCmd>>
{
    public UpdateHospitalLocationHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateHospitalLocationCmd>> Handle(UpdateHospitalLocationCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}