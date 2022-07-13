using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalServiceHandler : CommandBaseHandler, IRequestHandler<CreateHospitalServiceCmd, CmdResponse<CreateHospitalServiceCmd>>
{
    public CreateHospitalServiceHandler()
    {
        
    }
    public async Task<CmdResponse<CreateHospitalServiceCmd>> Handle(CreateHospitalServiceCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}