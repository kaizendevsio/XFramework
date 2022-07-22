using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalConsultationHandler : CommandBaseHandler, IRequestHandler<CreateHospitalConsultationCmd, CmdResponse<CreateHospitalConsultationCmd>>
{
    public CreateHospitalConsultationHandler()
    {
        
    }
    public async Task<CmdResponse<CreateHospitalConsultationCmd>> Handle(CreateHospitalConsultationCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}