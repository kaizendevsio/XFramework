using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateHospitalEntityGroupCmd, CmdResponse<CreateHospitalEntityGroupCmd>>
{
    public CreateHospitalEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalEntityGroupCmd>> Handle(CreateHospitalEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}