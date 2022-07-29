using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalServiceTagHandler : CommandBaseHandler, IRequestHandler<CreateHospitalServiceTagCmd, CmdResponse<CreateHospitalServiceTagCmd>>
{
    public CreateHospitalServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalServiceTagCmd>> Handle(CreateHospitalServiceTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}