using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalServiceEntityGroupCmd, CmdResponse<UpdateHospitalServiceEntityGroupCmd>>
{
    public UpdateHospitalServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalServiceEntityGroupCmd>> Handle(UpdateHospitalServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}