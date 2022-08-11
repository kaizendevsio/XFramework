using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalLocationHandler : CommandBaseHandler, IRequestHandler<CreateHospitalLocationCmd, CmdResponse<CreateHospitalLocationCmd>>
{
    public CreateHospitalLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalLocationCmd>> Handle(CreateHospitalLocationCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}