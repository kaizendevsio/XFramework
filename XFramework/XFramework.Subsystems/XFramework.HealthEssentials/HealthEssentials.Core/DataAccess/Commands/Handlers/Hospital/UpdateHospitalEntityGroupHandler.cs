using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalEntityGroupCmd, CmdResponse<UpdateHospitalEntityGroupCmd>>
{
    public UpdateHospitalEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalEntityGroupCmd>> Handle(UpdateHospitalEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}