using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalServiceEntityHandler : CommandBaseHandler, IRequestHandler<CreateHospitalServiceEntityCmd, CmdResponse<CreateHospitalServiceEntityCmd>>
{
    public CreateHospitalServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalServiceEntityCmd>> Handle(CreateHospitalServiceEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}