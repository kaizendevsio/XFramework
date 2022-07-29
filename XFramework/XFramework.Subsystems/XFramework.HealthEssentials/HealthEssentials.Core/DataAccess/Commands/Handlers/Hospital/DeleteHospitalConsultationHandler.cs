using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalConsultationHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalConsultationCmd, CmdResponse<DeleteHospitalConsultationCmd>>
{
    public DeleteHospitalConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalConsultationCmd>> Handle(DeleteHospitalConsultationCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}