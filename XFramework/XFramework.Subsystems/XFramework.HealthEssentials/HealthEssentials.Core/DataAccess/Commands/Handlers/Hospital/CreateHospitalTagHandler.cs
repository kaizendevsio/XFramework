using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalTagHandler : CommandBaseHandler, IRequestHandler<CreateHospitalTagCmd, CmdResponse<CreateHospitalTagCmd>>
{
    public CreateHospitalTagHandler()
    {
        
    }
    public async Task<CmdResponse<CreateHospitalTagCmd>> Handle(CreateHospitalTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}