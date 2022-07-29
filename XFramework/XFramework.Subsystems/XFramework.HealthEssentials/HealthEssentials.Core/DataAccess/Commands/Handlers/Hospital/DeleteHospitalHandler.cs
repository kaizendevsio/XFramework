using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalCmd, CmdResponse<DeleteHospitalCmd>>
{
    public DeleteHospitalHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalCmd>> Handle(DeleteHospitalCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}