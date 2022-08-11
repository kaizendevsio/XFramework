using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalLocationHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalLocationCmd, CmdResponse<DeleteHospitalLocationCmd>>
{
    public DeleteHospitalLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalLocationCmd>> Handle(DeleteHospitalLocationCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}