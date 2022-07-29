using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalLaboratoryHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalLaboratoryCmd, CmdResponse<DeleteHospitalLaboratoryCmd>>
{
    public DeleteHospitalLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalLaboratoryCmd>> Handle(DeleteHospitalLaboratoryCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}