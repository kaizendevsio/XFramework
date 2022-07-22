using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateHospitalServiceEntityGroupCmd, CmdResponse<CreateHospitalServiceEntityGroupCmd>>
{
    public CreateHospitalServiceEntityGroupHandler()
    {
        
    }
    public async Task<CmdResponse<CreateHospitalServiceEntityGroupCmd>> Handle(CreateHospitalServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}