using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalCmd, CmdResponse<UpdateHospitalCmd>>
{
    public UpdateHospitalHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalCmd>> Handle(UpdateHospitalCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}