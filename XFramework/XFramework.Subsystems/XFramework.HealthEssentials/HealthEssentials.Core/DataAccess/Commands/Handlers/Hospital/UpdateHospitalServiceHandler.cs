using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalServiceHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalServiceCmd, CmdResponse<UpdateHospitalServiceCmd>>
{
    public UpdateHospitalServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalServiceCmd>> Handle(UpdateHospitalServiceCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}