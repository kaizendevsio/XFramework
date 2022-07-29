using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalHandler : CommandBaseHandler, IRequestHandler<CreateHospitalCmd, CmdResponse<CreateHospitalCmd>>
{
    public CreateHospitalHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalCmd>> Handle(CreateHospitalCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}